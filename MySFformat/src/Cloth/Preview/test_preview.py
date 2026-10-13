"""Repeat native PoCs and check offline sockets, output refusal and version gate.

Requires psutil for child-PID socket observations. Assets must come from the
validated scene builder; arbitrary hand-written descriptions are unsupported.
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
import psutil
from preview_build import build_host


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime', required=True, type=Path)
    parser.add_argument('--assets', required=True, action='append', type=Path)
    args = parser.parse_args()
    source = Path(__file__).resolve().parent
    work = source.parents[3] / 'helper-ext/cloth/preview-work' / ('acceptance-' + time.strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8])
    work.mkdir(parents=True, exist_ok=False)
    exe = build_host(source, work)
    runtime = args.runtime.resolve()
    vendor_before = {str(p.relative_to(runtime)): digest(p) for p in runtime.rglob('*.dll')}
    env = dict(os.environ, DEVPATH=str(runtime / 'tools/gac'), PATH=str(Path(os.environ['WINDIR']) / 'System32'))
    results = []

    def run(name, folder, assets=None, expected=0):
        report = work / (name + '.json')
        command = [str(exe), str(folder), str(report)]
        if assets:
            command.extend([str(assets), '120'])
        observed = set()
        with (work / (name + '.log')).open('wb') as log:
            child = subprocess.Popen(command, env=env, cwd=work, stdout=log, stderr=log)
            deadline = time.monotonic() + 45
            while child.poll() is None:
                if time.monotonic() > deadline:
                    child.kill(); child.wait()
                    raise RuntimeError('Owned native child timed out: ' + name)
                try:
                    for connection in psutil.Process(child.pid).net_connections(kind='inet'):
                        if connection.laddr:
                            observed.add(str(connection))
                except psutil.NoSuchProcess:
                    pass
                time.sleep(0.01)
            assert child.returncode == expected, (name, child.returncode, work / (name + '.log'))
        assert not observed, (name, observed)
        data = json.loads(report.read_text(encoding='utf-8-sig'))
        if expected == 0:
            assert data['cleanupOk'] and data['vendorNetworkDisabled'] and data['guardedSocketImports'] > 0
            assert data['executionLevel'] == 'asInvoker'
            for snapshot in data['snapshots']:
                for value in snapshot['modules'] + snapshot['assemblies']:
                    p = Path(value)
                    if p.suffix.lower() == '.dll' and p.name.lower().startswith(('havok.', 'hct', 'hkcompat')):
                        assert p.resolve().is_relative_to(runtime), (name, p)
        results.append({'name': name, 'pass': True, 'observedInetSockets': list(observed),
                        'deniedSocketCalls': data['deniedSocketCalls']})
        return data, report

    for i, original in enumerate(args.assets):
        assets = work / ('fixture-' + str(i))
        assets.mkdir()
        for name in ['scene.hkx', 'cloth.hkx', 'description.json']:
            shutil.copy2(original / name, assets / name)
        before = {p.name: digest(p) for p in assets.iterdir()}
        simulations = []
        for repeat in range(2):
            data, report = run('fixture-' + str(i) + '-repeat-' + str(repeat), runtime, assets)
            assert data['status'] == 'PASS_NATIVE_CLOTH' and data['clothSimulationTested']
            sim = data['simulation']
            assert sim['finitePositions'] and sim['changedParticles'] > 0 and sim['maxDisplayComponentDelta'] > 0
            assert sim['clm2Loaded']
            simulations.append({k: sim[k] for k in ['particles', 'changedParticles', 'maxParticleComponentDelta',
                                                    'maxDisplayComponentDelta', 'runtimeRepacked', 'clm2Meshes', 'clm2Entries']})
        assert simulations[0] == simulations[1], ('nonrepeatable simulation', simulations)
        assert before == {name: digest(assets / name) for name in before}
    data, report = run('bootstrap-offline', runtime)
    before = digest(report)
    result = subprocess.run([str(exe), str(runtime), str(report)], env=env, capture_output=True, timeout=10)
    assert result.returncode == 2 and digest(report) == before
    results.append({'name': 'existing-report-refused', 'pass': True})
    bad = work / 'unsupported-runtime'
    (bad / 'tools').mkdir(parents=True)
    (bad / 'tools/hctPreviewPlugin.dll').write_bytes(b'Synthetic non-vendor invalid-version fixture')
    data, report = run('wrong-version-before-native-load', bad, expected=1)
    assert not data['nativeReady'] and not data['clothSimulationTested'] and data['pluginSha256'] != data['expectedPluginSha256']
    assert vendor_before == {str(p.relative_to(runtime)): digest(p) for p in runtime.rglob('*.dll')}
    (work / 'summary.json').write_text(json.dumps({'status': 'PASS', 'tests': results, 'vendorFilesUnchanged': True}, indent=2), encoding='utf-8')
    print('PASS offline sockets, repeated native movement, CLM2, version gate, report protection and unchanged vendor files')
    print(work)


if __name__ == '__main__':
    main()
