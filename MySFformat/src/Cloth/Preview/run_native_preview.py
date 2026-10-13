"""Build a geometry adapter and step unchanged authored cloth in the local HCT host."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import time
import uuid
import sys
from preview_build import build_host, modern_compiler
from scene_schema import merge


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime', required=True, type=Path)
    parser.add_argument('--cloth', required=True, type=Path)
    parser.add_argument('--flver', required=True, type=Path)
    parser.add_argument('--clm2', type=Path)
    parser.add_argument('--environment', type=Path)
    parser.add_argument('--frames', type=int, default=120)
    parser.add_argument('--timeout', type=int, default=90)
    args = parser.parse_args()
    if not 2 <= args.frames <= 3600:
        parser.error('frames must be between 2 and 3600')
    source = Path(__file__).resolve().parent
    repo = source.parents[3]
    app = repo / 'MySFformat'
    runtime = args.runtime.resolve()
    inputs = [args.cloth, args.flver] + ([args.clm2] if args.clm2 else [])
    for path in inputs + [runtime / 'tools/hctPreviewPlugin.dll']:
        if not path.is_file():
            parser.error('Missing input: ' + str(path))
    before = {str(p.resolve()): digest(p) for p in inputs}
    work = repo / 'helper-ext/cloth/preview-work' / ('simulation-' + time.strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8])
    work.mkdir(parents=True, exist_ok=False)
    merged = work / 'schema-bb-scene.xml'
    merged_ds3 = work / 'schema-ds3-scene.xml'
    merge(app / 'tools/Cloth/native-schema.xml', merged)
    merge(app / 'tools/Cloth/native-schema-ds3.xml', merged_ds3)
    exe = build_host(source, work)
    builder = work / 'PreviewAssetBuilder.exe'
    souls = app / 'bin/Debug/SoulsFormats.dll'
    shutil.copy2(souls, work / souls.name)
    for dependency in souls.parent.glob('System*.dll'):
        shutil.copy2(dependency, work / dependency.name)
    shutil.copy2(souls.parent / 'MySFformat.exe.config', work / 'PreviewAssetBuilder.exe.config')
    csc = modern_compiler()
    result = subprocess.run([str(csc), '/nologo', '/platform:x64', '/optimize+',
        '/win32manifest:' + str(source / 'host.manifest'), '/r:System.Web.Extensions.dll',
        '/r:System.Numerics.dll', '/r:System.Xml.Linq.dll', '/r:' + str(souls),
        '/resource:' + str(merged) + ',MySFformat.NativeClothSchema.xml',
        '/resource:' + str(merged_ds3) + ',MySFformat.NativeDs3ClothSchema.xml',
        '/out:' + str(builder), str(source / 'PreviewAssetBuilder.cs'), str(source / 'PreviewData.cs'),
        str(source / 'PreviewEnvironment.cs'),
        str(app / 'src/HkxPack/HkxPackfileCodec.cs')], capture_output=True, timeout=60)
    (work / 'build-assets.log').write_bytes(result.stdout + result.stderr)
    if result.returncode:
        raise RuntimeError('Asset builder compile failed; see ' + str(work / 'build-assets.log'))
    assets = work / 'assets'
    build_args = [str(builder), str(args.cloth.resolve()), str(args.flver.resolve()), str(merged), str(assets)]
    if args.environment:
        build_args.extend([str(args.clm2.resolve()) if args.clm2 else '', str(args.environment.resolve())])
    elif args.clm2:
        build_args.append(str(args.clm2.resolve()))
    result = subprocess.run(build_args, capture_output=True, timeout=args.timeout)
    (work / 'assets.log').write_bytes(result.stdout + result.stderr)
    if result.returncode:
        print((result.stdout + result.stderr).decode(errors='replace'))
        print('Asset preparation failed:', work)
        return 1
    print(result.stdout.decode(errors='replace').strip(), flush=True)
    env = dict(os.environ, DEVPATH=str(runtime / 'tools/gac'), PATH=str(Path(os.environ['WINDIR']) / 'System32'))
    report = work / 'simulation.json'
    try:
        result = subprocess.run([str(exe), str(runtime), str(report), str(assets), str(args.frames)],
                                capture_output=True, cwd=work, env=env, timeout=args.timeout)
        (work / 'simulation.log').write_bytes(result.stdout + result.stderr)
        exit_code = result.returncode
    except subprocess.TimeoutExpired as error:
        (work / 'simulation.log').write_bytes((error.stdout or b'') + (error.stderr or b'') + b'\nTIMEOUT')
        exit_code = 'TIMEOUT'
    after = {str(p.resolve()): digest(p) for p in inputs}
    data = json.loads(report.read_text(encoding='utf-8-sig')) if report.exists() else None
    modules = sorted({n for s in (data or {}).get('snapshots', []) for n in s['modules'] + s['assemblies']})
    vendor_external = [n for n in modules if Path(n).suffix.lower() == '.dll'
                       and Path(n).name.lower().startswith(('havok.', 'hct', 'hkcompat'))
                       and not Path(n).resolve().is_relative_to(runtime)]
    success = exit_code == 0 and (data or {}).get('status') == 'PASS_NATIVE_CLOTH' and before == after and not vendor_external
    summary = {'status': 'PASS_NATIVE_CLOTH_POC' if success else 'FAIL', 'exitCode': exit_code,
               'host': str(exe), 'runtime': str(runtime), 'work': str(work), 'sourceHashesBefore': before,
               'sourceHashesAfter': after, 'externalVendorModules': vendor_external,
               'simulation': (data or {}).get('simulation'), 'error': (data or {}).get('error')}
    summary['networkGuard'] = {key: (data or {}).get(key) for key in ['vendorNetworkDisabled', 'deniedSocketCalls', 'guardedSocketImports']}
    (work / 'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
    print(json.dumps(summary, indent=2))
    return 0 if success else 1


if __name__ == '__main__':
    raise SystemExit(main())
