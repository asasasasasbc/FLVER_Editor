import subprocess, pathlib, hashlib, json
from PIL import Image, ImageDraw
root=pathlib.Path(__file__).resolve().parents[1]
exe=root/'bin/Debug/MySFformat.exe'
work=root/'tests/hkx-work';work.mkdir(exist_ok=True)
chrroot=pathlib.Path('G:/bloodborne_pc-win/Games/dvdroot_ps4/chr')
skeleton=chrroot/'c0000-anibnd-dcx/chr/c0000/hkx/skeleton.hkx'
animation=chrroot/'c0000_a00_hi-anibnd-dcx/chr/c0000/hkx/a000/a000_104252.hkx'
models={'native':pathlib.Path('G:/bloodborne_pc-win/MOD/work/rig-audit/native-bd.flver'),'mod':pathlib.Path('G:/bloodborne_pc-win/MOD/work/editor-preview/Saber-v3-direction-preview.flver')}
checksum=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
inputs=[*models.values(),skeleton,animation];before=[checksum(p) for p in inputs]
shots=[];results=[]
for model_name,model in models.items():
 for view,camera in [('front',[0,1,-3.5]),('side',[3.5,1,0]),('back',[0,1,3.5])]:
  config=work/f'{view}.view.json';config.write_text(json.dumps(dict(camera=camera,target=[0,1,0],renderMode='Triangle',showBones=True,showDummies=False)))
  for frame in (0,35,70):
   out=work/f'{model_name}-{view}-{frame}.png'
   if out.exists():out.unlink()
   p=subprocess.run([str(exe),'--render',str(model),'--camera',str(config),'--screenshot',str(out),'--hkx-skeleton',str(skeleton),'--hkx-animation',str(animation),'--hkx-frame',str(frame),'--width','640','--height','800'],cwd=exe.parent,capture_output=True,text=True,timeout=60)
   assert p.returncode==0,(p.returncode,p.stdout,p.stderr)
   assert out.is_file(),'HKX render missing'
   receipt=json.loads(pathlib.Path(str(out)+'.json').read_text());assert receipt['hkxFrame']==frame
   shots.append(out);results.append(dict(model=model_name,view=view,frame=frame,sha256=checksum(out),log=p.stdout))
   print('PASS GPU render',model_name,view,frame,p.stdout.strip(),flush=True)
assert len({r['sha256'] for r in results})==18,'Distinct view/frame combinations rendered identical images'
for args in [['--hkx-skeleton',str(skeleton)],['--hkx-frame','-1'],['--hkx-skeleton',str(skeleton),'--hkx-animation',str(skeleton)],['--hkx-skeleton',str(skeleton),'--hkx-animation',str(work/'missing.hkx')]]:
 out=work/'invalid.png'
 if out.exists():out.unlink()
 p=subprocess.run([str(exe),'--render',str(models['native']),'--screenshot',str(out)]+args,cwd=exe.parent,capture_output=True,text=True,timeout=30)
 assert p.returncode!=0 and not out.exists(),('invalid input was accepted',args)
 print('PASS invalid input rejected',args,flush=True)
assert before==[checksum(p) for p in inputs],'Preview modified input files'
for model in models:
 selected=[p for p in shots if p.name.startswith(model+'-')]
 sheet=Image.new('RGB',(960,1260),'white');draw=ImageDraw.Draw(sheet)
 for i,p in enumerate(selected):
  x=(i%3)*320;y=(i//3)*420
  sheet.paste(Image.open(p).resize((320,400)),(x,y+20));draw.text((x+5,y+4),p.stem,fill='black')
 sheet.save(work/f'{model}-contact-sheet.png')
report=dict(renders=results,invalid_cases_rejected=4,input_files_unchanged=True,inputs=[dict(path=str(p),sha256=checksum(p)) for p in inputs])
(work/'verification.json').write_text(json.dumps(report,indent=2))
print('PASS: 18 distinct animated GPU renders, four error cases, source files unchanged')
