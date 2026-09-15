"""Run the built Mac game's behavioral diagnostics, including a complete campaign."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import json, subprocess, sys
root=Path(__file__).resolve().parents[1]
output=root/"Verification/v0.3"
app=root/"Builds/Mac/Frontier Defense.app/Contents/MacOS/Frontier Defense"
def run(mode):
    path=output/f"{mode}.json"
    path.unlink(missing_ok=True)
    result=subprocess.run([str(app),"-batchmode","-nographics",f"--frontier-check={mode}",f"--frontier-output={path}","-logFile",str(output/f"{mode}.log")],stdout=subprocess.DEVNULL,stderr=subprocess.STDOUT,timeout=180)
    data=json.loads(path.read_text()) if path.exists() else {"passed":False,"failures":["No diagnostic report was produced"]}
    return {"mode":mode,"exitCode":result.returncode,"passed":result.returncode==0 and data.get("passed",False),"checks":len(data.get("checks",[])),"failures":data.get("failures",[]),"spawned":data.get("spawned"),"kills":data.get("kills"),"simulatedSeconds":data.get("simulatedSeconds")}
with ThreadPoolExecutor(max_workers=3) as pool:
    results=list(pool.map(run,["mechanics","fortification","squad","victory","afk","defeat"]))
print(json.dumps(results,indent=2))
sys.exit(0 if all(r["passed"] for r in results) else 2)
