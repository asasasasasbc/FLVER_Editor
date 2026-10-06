from pathlib import Path
import json,subprocess
import numpy as np
from PIL import Image
W=Path('G:/bloodborne_pc-win/MOD/work/editor-preview');O=W/'aspect-check';O.mkdir(exist_ok=True)
exe=Path('D:/FLVER_Editor/MySFformat/bin/Debug/MySFformat.Cli.exe')
model=W/'Saber-v3-direction-preview.flver';camera=W/'cli-shots/front-solid.view.json'
results=[]
for width,height in [(1080,1080),(1920,1080),(900,1080)]:
 out=O/f'{width}x{height}.png'
 r=subprocess.run([str(exe),'--render',str(model),'--camera',str(camera),'--screenshot',str(out),'--width',str(width),'--height',str(height)],cwd=exe.parent,capture_output=True,text=True,timeout=30)
 assert r.returncode==0,r.stderr
 m=json.loads(Path(str(out)+'.json').read_text());a=np.array(Image.open(out).convert('RGB'))
 # Exclude blue background and colored ground axes; measure opaque gray model faces only.
 mask=(a.max(axis=2).astype(int)-a.min(axis=2).astype(int))<3
 ys,xs=np.where(mask);assert len(xs)>1000
 bbox=[int(xs.min()),int(ys.min()),int(xs.max()),int(ys.max())]
 result=dict(size=[width,height],projection=m['projectionAspect'],viewport=m['viewportAspect'],preferred=m['preferredAspect'],bbox=bbox,model_pixel_width=bbox[2]-bbox[0]+1,model_pixel_height=bbox[3]-bbox[1]+1,output=str(out))
 print(result,flush=True);results.append(result)
(O/'measured.json').write_text(json.dumps(results,indent=2))
for m in results:
 assert abs(m['projection']-m['size'][0]/m['size'][1])<1e-5,'Projection aspect does not match actual PNG viewport'
 assert abs(m['projection']-m['viewport'])<1e-5,'Projection/viewport aspect disagree'
w=[m['model_pixel_width'] for m in results];h=[m['model_pixel_height'] for m in results]
assert max(w)-min(w)<=2,('Changing width squeezed model',w)
assert max(h)-min(h)<=2,('Changing width changed model height',h)
print('PASS: same vertical FOV and output height -> model pixel proportions stable across square, wide and portrait canvases')
