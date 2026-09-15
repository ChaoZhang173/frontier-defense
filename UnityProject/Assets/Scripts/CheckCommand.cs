using UnityEngine;

namespace Frontier
{
    // Fixture positioning for isolated diagnostics; purchases still use real terminal access.
    public static class CheckCommand
    {
        public static bool Enter(GameWorld world)
        {
            world.SetPaused(false);
            if(world.Fort.PlayerInside)world.Fort.ExitPlayer();
            FortificationChecks.PlacePlayer(world,new Vector3(0,0,3));
            return world.TryOpenCommandTerminal();
        }
        public static void Leave(GameWorld world)=>world.SetPaused(false);
    }
}
