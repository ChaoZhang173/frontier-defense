"""Build the offline Xiaohongshu artifact from the audited static files."""
from pathlib import Path
import hashlib, json, zipfile
root=Path(__file__).resolve().parent.parent
dist=root/'XhsMiniTool/dist'
allowed={'.html','.css','.js','.json','.png','.jpg','.jpeg','.gif','.webp','.svg','.woff','.woff2'}
files=sorted(p for p in dist.rglob('*') if p.is_file())
assert (dist/'index.html').is_file()
assert sum(p.name=='index.html' for p in files)==1
assert all(p.suffix.lower() in allowed for p in files)
out=root/'Releases/FrontierDefense-0.7.0-XHS.zip'
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
    for p in files: z.write(p,p.relative_to(dist))
assert out.stat().st_size<=10*1024*1024
manifest={'version':'0.7.0','zip':str(out),'zip_bytes':out.stat().st_size,'sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'files':{str(p.relative_to(dist)):{'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in files},'validation_limit':'Automated game logic, input/geometry regressions and desktop Chrome viewport tests; local ZIP structure audit. v0.7.0 has not been uploaded or tested on an iPhone or Chrome 61 runtime.'}
(root/'Verification/xhs-v0.7/release-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2))
print(str(out));print(str(out.stat().st_size)+' bytes')
