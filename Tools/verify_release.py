"""Run the built Mac game's behavioral diagnostics, including a complete campaign."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import argparse, json, subprocess, sys
root=Path(__file__).resolve().parents[1]
output=root/"Verification/v0.5.1"
output.mkdir(parents=True,exist_ok=True)
app=root/"Builds/Mac/Frontier Defense.app/Contents/MacOS/Frontier Defense"
def run(mode):
    path=output/f"{mode}.json"
    path.unlink(missing_ok=True)
    result=subprocess.run([str(app),"-batchmode","-nographics",f"--frontier-check={mode}",f"--frontier-output={path}","-logFile",str(output/f"{mode}.log")],stdout=subprocess.DEVNULL,stderr=subprocess.STDOUT,timeout=180)
    data=json.loads(path.read_text()) if path.exists() else {"passed":False,"failures":["No diagnostic report was produced"]}
    return {"mode":mode,"exitCode":result.returncode,"passed":result.returncode==0 and data.get("passed",False),"checks":len(data.get("checks",[])),"failures":data.get("failures",[]),"spawned":data.get("spawned"),"kills":data.get("kills"),"simulatedSeconds":data.get("simulatedSeconds"),"outcome":data.get("outcome"),"hqHealth":data.get("hqHealth"),"peakEnemies":data.get("peakEnemies"),"peakFrontlineEnemies":data.get("peakFrontlineEnemies"),"headquartersDamage":data.get("headquartersDamage"),"bunkerDamage":data.get("bunkerDamage"),"guardLosses":data.get("guardLosses"),"squadLosses":data.get("squadLosses"),"endingSupplies":data.get("endingSupplies")}
modes=["assault","balance_rifle","balance_squad","interaction","guards","navigation","construction","mechanics","fortification","squad","victory","afk","defeat"]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument("modes",nargs="*",metavar="MODE",help="Diagnostic modes; omit to run the full release suite")
args=parser.parse_args()
if any(mode not in modes for mode in args.modes):parser.error("Mode must be one of: "+", ".join(modes))
with ThreadPoolExecutor(max_workers=3) as pool:
    results=list(pool.map(run,args.modes or modes))
print(json.dumps(results,indent=2))
sys.exit(0 if all(r["passed"] for r in results) else 2)
