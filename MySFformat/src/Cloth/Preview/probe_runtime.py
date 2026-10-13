"""Build an isolated HCT bootstrap probe; collect/test a local DLL-only subset.

No binary redistribution. No game launch, installer, vendor-file modification or
decompilation. Existing files are never overwritten; every run gets a new folder.
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import time
import uuid
from preview_build import build_host


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def under(path, root):
    try:
        return path.resolve().relative_to(root.resolve())
    except ValueError:
        return None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime', required=True, type=Path)
    parser.add_argument('--timeout', type=int, default=60)
    args = parser.parse_args()
    source = Path(__file__).resolve().parent
    repo = source.parents[3]
    runtime = args.runtime.resolve()
    if not (runtime / 'tools/hctPreviewPlugin.dll').is_file():
        parser.error('runtime must contain tools/hctPreviewPlugin.dll')
    work = repo / 'helper-ext/cloth/preview-work' / (time.strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8])
    work.mkdir(parents=True, exist_ok=False)
    exe = build_host(source, work)
    results = []

    def probe(label, folder):
        report = work / (label + '.json')
        env = dict(os.environ)
        env['PATH'] = str(Path(os.environ['WINDIR']) / 'System32')
        env['DEVPATH'] = str(folder / 'tools/gac')
        started = time.monotonic()
        try:
            process = subprocess.run([str(exe), str(folder), str(report)], cwd=work,
                                     env=env, capture_output=True, timeout=args.timeout)
            (work / (label + '.log')).write_bytes(process.stdout + process.stderr)
            code = process.returncode
        except subprocess.TimeoutExpired as error:
            # subprocess.run terminates only its owned probe process on timeout.
            (work / (label + '.log')).write_bytes((error.stdout or b'') + (error.stderr or b'') + b'\nTIMEOUT')
            code = 'TIMEOUT'
        data = json.loads(report.read_text(encoding='utf-8-sig')) if report.exists() else None
        results.append({'stage': label, 'exitCode': code, 'seconds': time.monotonic() - started,
                        'report': str(report), 'status': data.get('status') if data else 'NO_REPORT'})
        print(label, results[-1]['status'], 'exit', code, flush=True)
        (work / 'summary.json').write_text(json.dumps({'stages': results}, indent=2), encoding='utf-8')
        return data if code == 0 else None

    baseline = probe('installed-baseline', runtime)
    if baseline is None:
        print('STOP: bootstrap failed. Evidence:', work)
        return 1
    dlls = {}
    for snapshot in baseline['snapshots']:
        for name in snapshot['modules'] + snapshot['assemblies']:
            path = Path(name)
            relative = under(path, runtime)
            if relative is not None and path.suffix.lower() == '.dll':
                dlls[str(relative).lower()] = (path, relative)
            elif path.name.lower().startswith('havok.') and path.suffix.lower() == '.dll':
                local = runtime / 'tools/gac' / path.name
                if not local.is_file() or sha256(local) != sha256(path):
                    raise RuntimeError('External vendor assembly does not match installation: ' + str(path))
                relative = Path('tools/gac') / path.name
                dlls[str(relative).lower()] = (local, relative)
    # Trace-driven subset, not a mathematical minimum. Includes only observed
    # vendor modules; initialization tests must confirm deferred loads as well.
    subset = work / 'runtime-subset'
    subset.mkdir()
    manifest = []
    for path, relative in sorted(dlls.values(), key=lambda pair: str(pair[1]).lower()):
        dest = subset / relative
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, dest)
        manifest.append({'path': str(relative), 'bytes': path.stat().st_size, 'sha256': sha256(path)})
    notice = runtime / 'EULA.txt'
    if notice.exists():
        shutil.copy2(notice, subset / notice.name)
    (work / 'runtime-manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    trimmed = probe('dll-subset', subset)
    if trimmed is not None:
        leaked = sorted(set(name for snap in trimmed['snapshots']
                            for name in snap['modules'] + snap['assemblies']
                            if under(Path(name), runtime) is not None))
        vendor_external = sorted(set(name for snap in trimmed['snapshots']
                                     for name in snap['modules'] + snap['assemblies']
                                     if (Path(name).suffix.lower() == '.dll'
                                         and Path(name).name.lower().startswith(('havok.', 'hct', 'hkcompat'))
                                         and under(Path(name), subset) is None)))
        passed = not leaked and not vendor_external
    else:
        leaked, vendor_external, passed = [], [], False
    summary = {'status': 'PASS_ISOLATED_BOOTSTRAP' if passed else 'FAIL_SUBSET',
               'stages': results, 'dllCount': len(manifest), 'dllBytes': sum(m['bytes'] for m in manifest),
               'installationFallbacks': leaked, 'externalVendorModules': vendor_external,
               'clothSimulationTested': False, 'graphicsRequired': True,
               'minimalityProven': False, 'work': str(work)}
    (work / 'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
    print(json.dumps(summary, indent=2))
    return 0 if passed else 1


if __name__ == '__main__':
    raise SystemExit(main())
