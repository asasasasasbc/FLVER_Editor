import pathlib,subprocess,json,hashlib
from PIL import Image,ImageDraw
root=pathlib.Path(__file__).resolve().parents[1];work=root/'tests/hkx-work';exe=root/'bin/Debug/MySFformat.exe'
models={'native':pathlib.Path('G:/bloodborne_pc-win/Games/dvdroot_ps4/parts/bd_f_2200.partsbnd.dcx'),'saber':pathlib.Path('G:/bloodborne_pc-win/MOD/Saber-Hunter-v3/dvdroot_ps4/parts/bd_f_2200.partsbnd.dcx')}
report=[]
for name,model in models.items():
 before=hashlib.sha256(model.read_bytes()).hexdigest();shots=[]
 for view,cam in [('front',[0,1,-3.5]),('side',[3.5,1,0]),('back',[0,1,3.5])]:
  camera=work/f'texture-{view}.view.json';camera.write_text(json.dumps(dict(camera=cam,target=[0,1,0],renderMode='TexOnly',showBones=False,showDummies=False)))
  out=work/f'{name}-textured-{view}.png'
  p=subprocess.run([str(exe),'--render',str(model),'--camera',str(camera),'--screenshot',str(out),'--width','800','--height','1000'],cwd=exe.parent,capture_output=True,text=True,timeout=90)
  print(p.stdout,p.stderr);assert p.returncode==0
  receipt=json.loads(pathlib.Path(str(out)+'.json').read_text());assert receipt.get('loadedTextures',0)==(10 if name=='native' else 12),receipt
  assert receipt.get('textureErrors')==0 and receipt.get('texturedMeshes',0)>0,receipt
  assert before==hashlib.sha256(model.read_bytes()).hexdigest()
  shots.append(out);report.append(dict(name=name,view=view,receipt=receipt,source_sha256=before))
  print('PASS automatic textured GPU render',name,view,receipt['loadedTextures'],receipt['texturedMeshes'],'textured meshes')
 sheet=Image.new('RGB',(1200,520),'white');draw=ImageDraw.Draw(sheet)
 for i,shot in enumerate(shots):sheet.paste(Image.open(shot).resize((400,500)),(i*400,20));draw.text((i*400+5,4),shot.stem,fill='black')
 sheet.save(work/f'{name}-texture-sheet.png')
(work/'bloodborne-texture-verification.json').write_text(json.dumps(report,indent=2))
assert len(report)==6
