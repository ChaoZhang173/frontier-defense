from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import subprocess,json,sys,time
root=Path(__file__).resolve().parents[1]
out=root/'Verification/v0.7';out.mkdir(exist_ok=True)
exe=root/'Builds/Mac/Frontier Defense.app/Contents/MacOS/Frontier Defense'
modes=sys.argv[1:] or ['mechanics','enemy_bases','expedition','repair','central','navigation','interaction','squad','guards']
def run(mode):
 path=out/(mode+'.json');log=out/(mode+'.log');started=time.monotonic()
 if path.exists():path.unlink()
 try:
  p=subprocess.run([str(exe),'-batchmode','-nographics','-logFile',str(log),'--frontier-check='+mode,'--frontier-output='+str(path)],cwd=root,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=180)
  if not path.exists():return dict(mode=mode,passed=False,exit=p.returncode,error=p.stdout.decode(errors='replace')[-2000:])
  r=json.loads(path.read_text());return dict(mode=mode,passed=r.get('passed',False),checks=len(r.get('checks',[])),failures=r.get('failures',[]),outcome=r.get('outcome'),seconds=round(time.monotonic()-started,2),exit=p.returncode)
 except Exception as e:return dict(mode=mode,passed=False,error=str(e))
with ThreadPoolExecutor(max_workers=3) as pool:results=list(pool.map(run,modes))
for row in results:print(json.dumps(row,ensure_ascii=False),flush=True)
report=dict(results=results,passed=all(r['passed'] for r in results))
(out/'checks-latest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
raise SystemExit(0 if report['passed'] else 1)
