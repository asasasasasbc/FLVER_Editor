"""Synchronous local acceptance run; no background workers or game writes."""
from pathlib import Path
import subprocess, json, sys
root=Path(__file__).resolve().parents[1]
tests=root/'tests'; work=tests/'hkx-work';work.mkdir(exist_ok=True)
exe=root/'bin/Debug/MySFformat.exe'
msbuild=Path('C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe')
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
chrroot=Path('G:/bloodborne_pc-win/Games/dvdroot_ps4/chr')
skeleton=chrroot/'c0000-anibnd-dcx/chr/c0000/hkx/skeleton.hkx'
animation=chrroot/'c0000_a00_hi-anibnd-dcx/chr/c0000/hkx/a000/a000_104252.hkx'
model=Path('G:/bloodborne_pc-win/MOD/work/rig-audit/native-bd.flver')
results=[]
def run(name,args,cwd=root,timeout=120):
 p=subprocess.run([str(a) for a in args],cwd=cwd,capture_output=True,timeout=timeout)
 text=p.stdout.decode('utf-8',errors='replace')+p.stderr.decode('utf-8',errors='replace')
 (work/(name+'.log')).write_text(text,encoding='utf-8')
 results.append(dict(name=name,exit_code=p.returncode,log=str(work/(name+'.log'))))
 print(name,'PASS' if p.returncode==0 else 'FAIL',flush=True)
 if p.returncode: print(text); raise RuntimeError(name+' failed')
try:
 run('build',[msbuild,'MySFformat.csproj','-p:Configuration=Debug','-v:quiet','-nologo'])
 for name in ['HkxTests','PlaybackTests','HkxUiIntegration','HkxViewerIntegration']:
  run('compile-'+name,[csc,'-nologo','-r:System.Numerics.dll','-r:System.Windows.Forms.dll','-r:System.Drawing.dll','-r:Microsoft.CSharp.dll','-out:'+name+'.exe',name+'.cs'],tests)
 run('decode-binding',[tests/'HkxTests.exe',exe,skeleton,animation,root/'tools/HavokToolset'])
 run('timeline',[tests/'PlaybackTests.exe',exe])
 run('player-controls',[tests/'HkxUiIntegration.exe',exe,model,skeleton,animation,work/'player.png'])
 for i in range(3):
  run('viewer-main-thread-resize-'+str(i+1),[tests/'HkxViewerIntegration.exe',exe,model,skeleton,animation],timeout=35)
 for name in ['RenderCliTests','CameraSettingsTests']:
  path=root.parent/'helper-tests'/name
  if path.with_suffix('.exe').exists():run('existing-'+name,[path.with_suffix('.exe'),exe])
 run('gpu-18-views',[sys.executable,tests/'verify_hkx_render.py'],timeout=240)
finally:
 (work/'test-summary.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print('PASS: all acceptance stages completed; see tests/hkx-work/test-summary.json')
