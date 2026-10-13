"""Build the process-isolated host into a stable, owned path (no elevation)."""
from pathlib import Path
import os
import shutil
import subprocess
import uuid


CONFIG = ('<?xml version="1.0"?><configuration><runtime>'
          '<developmentMode developerInstallation="true"/>'
          '</runtime></configuration>')


def modern_compiler():
    vswhere = Path(os.environ['ProgramFiles(x86)']) / 'Microsoft Visual Studio/Installer/vswhere.exe'
    result = subprocess.run([str(vswhere), '-latest', '-products', '*', '-find',
                             'MSBuild/**/Roslyn/csc.exe'], capture_output=True, text=True, timeout=15)
    paths = [Path(line.strip()) for line in result.stdout.splitlines() if line.strip()]
    if result.returncode or not paths:
        raise RuntimeError('A Visual Studio Roslyn C# compiler is required for the scene builder.')
    return paths[0]


def build_host(source, work):
    repo = source.parents[3]
    folder = repo / 'helper-ext/cloth/preview-host'
    folder.mkdir(parents=True, exist_ok=True)
    built = work / 'HctPreviewProbe.exe'
    csc = Path(os.environ['WINDIR']) / 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    command = [str(csc), '/nologo', '/platform:x64', '/unsafe', '/optimize+',
               '/win32manifest:' + str(source / 'host.manifest'),
               '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll',
               '/r:System.Web.Extensions.dll', '/r:System.Numerics.dll',
               '/out:' + str(built)]
    command.extend(str(source / name) for name in ['HctPreviewProbe.cs', 'NativeSimulation.cs', 'PreviewData.cs', 'HostNetworkGuard.cs', 'PreviewEnvironment.cs', 'HostEnvironment.cs'])
    result = subprocess.run(command, capture_output=True, timeout=60)
    (work / 'build.log').write_bytes(result.stdout + result.stderr)
    if result.returncode:
        raise RuntimeError('Host build failed; see ' + str(work / 'build.log'))
    target = folder / built.name
    if not target.exists() or target.read_bytes() != built.read_bytes():
        stage = folder / ('.host-' + uuid.uuid4().hex + '.tmp')
        shutil.copy2(built, stage)
        try:
            os.replace(stage, target)
        finally:
            if stage.exists():
                stage.unlink()
    config = folder / (built.name + '.config')
    if not config.exists() or config.read_text(encoding='utf-8') != CONFIG:
        config.write_text(CONFIG, encoding='utf-8')
    return target
