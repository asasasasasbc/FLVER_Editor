from pathlib import Path
import hashlib,json,subprocess
import numpy as np
from PIL import Image
W=Path('G:/bloodborne_pc-win/MOD/work/editor-preview')
EXE=Path('D:/FLVER_Editor/MySFformat/bin/Debug/MySFformat.Cli.exe')
MODEL=W/'Saber-v3-direction-preview.flver'
OUT=W/'cli-shots';OUT.mkdir(exist_ok=True)
checksum=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
before=checksum(MODEL)
views={
 'front-solid':([0,1.05,-3.4],[0,1.05,0],'Triangle',False),
 'side-solid':([3.4,1.05,0],[0,1.05,0],'Triangle',False),
 'back-solid':([0,1.05,3.4],[0,1.05,0],'Triangle',False),
 'waist-front':([0,1.05,-1.15],[0,1.05,0],'BothNoTex',True),
 'waist-side':([1.15,1.05,0],[0,1.05,0],'BothNoTex',True),
 'waist-back':([0,1.05,1.15],[0,1.05,0],'BothNoTex',True),
}
results=[]
for name,(camera,target,mode,bones) in views.items():
 config=OUT/(name+'.view.json');config.write_text(json.dumps(dict(camera=camera,target=target,renderMode=mode,showBones=bones,showDummies=False),indent=2))
 output=OUT/(name+'.png')
 r=subprocess.run([str(EXE),'--render',str(MODEL),'--camera',str(config),'--screenshot',str(output),'--width','1000','--height','1200'],cwd=EXE.parent,capture_output=True,text=True,timeout=30)
 assert r.returncode==0,(name,r.returncode,r.stderr)
 a=np.array(Image.open(output).convert('RGB'));n=int(np.sum(np.any(a!=a[0,0],axis=2)))
 assert a.shape==(1200,1000,3) and n>10000,(name,a.shape,n)
 receipt=json.loads(Path(str(output)+'.json').read_text());assert receipt['camera']==camera and receipt['renderMode']==mode
 results.append(dict(view=name,output=str(output),non_background_pixels=n,sha256=checksum(output)))
 print(name,'PASS',output.stat().st_size,'bytes',n,'non-background pixels',flush=True)
assert len({r['sha256'] for r in results})==len(results),'Different viewpoints produced identical images'
# Real entrypoint must reject malformed CLI without opening an editor or waiting for user input.
for args in [['--screenshot',str(OUT/'should-not-exist.png')],['--render',str(MODEL),'--screenshot',str(OUT/'bad-size.png'),'--width','0'],['--render',str(MODEL),'--screenshot',str(OUT/'bad-camera.png'),'--camera',str(OUT/'missing.json')]]:
 r=subprocess.run([str(EXE)]+args,cwd=EXE.parent,capture_output=True,text=True,timeout=10)
 assert r.returncode!=0,'Invalid invocation succeeded'
 output=Path(args[args.index('--screenshot')+1]);assert not output.exists(),'Failed invocation wrote a screenshot'
 print('Invalid invocation rejected without dialog:',args[0],flush=True)
assert checksum(MODEL)==before,'Renderer modified input model'
(OUT/'cli-verification.json').write_text(json.dumps(dict(model=str(MODEL),model_sha256_before=before,model_sha256_after=checksum(MODEL),views=results,invalid_invocations_rejected=3),indent=2))
print('PASS: six distinct GPU images, three error cases, input model unchanged')
