from pathlib import Path
from html.parser import HTMLParser
import hashlib,json,re,zipfile
root=Path(__file__).resolve().parents[2]
dist=root/'XhsMiniTool/dist'
release=root/'Releases/FrontierDefense-0.7.0-XHS.zip'
class ResourceParser(HTMLParser):
 def __init__(self):super().__init__();self.urls=[]
 def handle_starttag(self,tag,attrs):
  data=dict(attrs)
  if tag=='script' and data.get('src'):self.urls.append(data['src'])
  if tag=='link' and data.get('href'):self.urls.append(data['href'])
p=ResourceParser();p.feed((dist/'index.html').read_text())
checks=[]
def check(name,result,**data):checks.append(dict(name=name,pass_=bool(result),**data))
files=sorted(x for x in dist.rglob('*') if x.is_file())
check('All page scripts and styles are local files',all(not re.match(r'^(https?:)?//',u) and (dist/u).is_file() for u in p.urls),urls=p.urls)
check('Support module loads before core and application',p.urls.index('./support.js')<p.urls.index('./core.js')<p.urls.index('./app.js'))
check('Version is visible on start screen','v0.7.0' in (dist/'index.html').read_text())
code='\n'.join((dist/n).read_text() for n in ['core.js','support.js','scene.js','app.js','menu-scroll.js'])
check('Game code has no network or persistent account calls',not re.search(r'\b(?:fetch|XMLHttpRequest|WebSocket|Worker|sendBeacon|localStorage|sessionStorage)\s*\(',code))
check('Original vendor license included',(dist/'licenses.json').is_file())
with zipfile.ZipFile(release) as z:
 check('Archive CRC integrity',z.testzip() is None)
 check('One root index.html',z.namelist().count('index.html')==1 and sum(n.endswith('index.html') for n in z.namelist())==1)
 check('ZIP matches tested static files byte for byte',set(z.namelist())=={str(f.relative_to(dist)) for f in files} and all(z.read(str(f.relative_to(dist)))==f.read_bytes() for f in files))
check('ZIP is under 10 MiB',release.stat().st_size<=10*1024*1024,bytes=release.stat().st_size)
for c in checks:c['pass']=c.pop('pass_')
report={'method':'Local packaging, source-reference and ZIP byte audit; not a platform upload review.','version':'0.7.0','zipSha256':hashlib.sha256(release.read_bytes()).hexdigest(),'checks':checks,'pass':all(c['pass'] for c in checks)}
Path(__file__).with_suffix('.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(report,ensure_ascii=False,indent=2));raise SystemExit(0 if report['pass'] else 1)
