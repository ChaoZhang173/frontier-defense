using System;
using UnityEngine;

namespace Frontier
{
    public static class InteractionChecks
    {
        public static void Run(GameWorld w,Action<bool,string> check)
        {
            w.BeginCampaign();Physics.SyncTransforms();
            Health guard=NextGuard(w);AimAt(w,guard,3);
            int credits=w.Credits,count=w.MarinesAlive;guard.Current=93;
            check(w.GuardRecruitTarget==guard,"The real interaction ray identifies the guard under the crosshair within four metres");
            check(w.Interact()&&guard.Role==Role.Squad&&w.Squad.Members[2].Self==guard&&guard.Current==93&&w.Credits==credits&&w.MarinesAlive==count-1,
                "Pressing the shared E interaction transfers the exact wounded guard without payment and releases one garrison slot");
            Health second=NextGuard(w);AimAt(w,second,5);
            check(w.GuardRecruitTarget==null&&!w.Squad.CanRecruitGuard(second),"A targeted guard beyond four metres cannot be recruited");
            AimAt(w,second,3);
            Vector3 center=Vector3.Lerp(w.Player.View.transform.position,second.Aim,.8f);
            Transform wall=Art.Part(w.transform,"Recruitment obstruction",center,new Vector3(2,3,.35f),2,collision:true);
            Physics.SyncTransforms();
            check(w.GuardRecruitTarget==null,"A real world collider blocks recruitment through a wall");
            wall.gameObject.SetActive(false);UnityEngine.Object.Destroy(wall.gameObject);Physics.SyncTransforms();
            w.SetPaused(true);check(w.GuardRecruitTarget==null&&!w.Squad.RecruitGuard(second),"A general pause prevents guard targeting and transfer");w.SetPaused(false);
            AimAt(w,second,3);check(w.Interact()&&w.Squad.RecruitedGuardCount==2&&w.Squad.Contracts==0,"Two guards may be recruited with E before hiring either mercenary");
            Health third=NextGuard(w);AimAt(w,third,3);credits=w.Credits;count=w.MarinesAlive;
            check(w.GuardRecruitTarget==third&&w.Interact()&&third.Role==Role.Marine&&w.MarinesAlive==count&&w.Credits==credits&&w.Squad.Count==2,
                "E on a third guard reports the recruit limit without spending supplies or consuming a base defender");
            FortificationChecks.PlacePlayer(w,new Vector3(0,0,3));w.Player.Yaw=180;w.Player.UpdateCamera(1);
            check(w.Interact()&&w.CommandTerminalOpen&&w.Paused,"E still opens the command terminal when no guard is targeted");
            check(w.Interact()&&!w.CommandTerminalOpen&&!w.Paused,"E closes the command terminal and resumes the battle");
            FortificationChecks.PlacePlayer(w,w.Fort.EntranceFor(w.Bunker));w.Player.Yaw=0;w.Player.UpdateCamera(1);
            check(w.Interact()&&w.Fort.PlayerInside,"E still enters the nearby bunker after guard recruitment");
            check(w.Interact()&&!w.Fort.PlayerInside,"E while stationed always exits the player's bunker");
        }
        static Health NextGuard(GameWorld w)=>w.Entities.Find(h=>h!=null&&h.Alive&&h.Role==Role.Marine&&!h.IsSheltered);
        static void AimAt(GameWorld w,Health guard,float distance)
        {
            FortificationChecks.PlacePlayer(w,guard.transform.position+Vector3.back*distance);
            w.Player.Yaw=0;w.Player.Pitch=8;w.Player.UpdateCamera(1);
            w.Player.View.transform.LookAt(guard.Aim);Physics.SyncTransforms();
        }
    }
}
