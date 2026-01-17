using Verse;
using RimWorld;
using RimWorld.Planet;

namespace PauseOtherSettlementsSimulation
{
    public static class LocalTimeManager
    {
        public static int GetLocalTicks(Map map)
        {
            var settings = PauseOtherSettlementsSimulation.Settings;
            if (!settings.enableLocalTimeSystem) return Find.TickManager.TicksGame;

            if (map == null) return Find.TickManager.TicksGame;

            var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();
            if (worldComp == null) return Find.TickManager.TicksGame;

            int globalTicks = Find.TickManager.TicksGame;
            int totalPaused = 0;

            if (worldComp.mapTotalPausedTicks.TryGetValue(map.uniqueID, out int storedPaused))
            {
                totalPaused += storedPaused;
                if (map.uniqueID == 2) Log.Message($"[Debug] GetLocalTicks(ID:2) Found storedPaused: {storedPaused}"); 
            }
            else if (map.uniqueID == 2)
            {
                Log.Message($"[Debug] GetLocalTicks(ID:2) NO storedPaused found.");
            }

            // If currently paused, add the duration of the current pause
            if (worldComp.mapLastPauseTick.TryGetValue(map.uniqueID, out int lastPauseTick) && lastPauseTick >= 0)
            {
                // Ensure we don't produce negative duration if TicksGame is somehow behind lastPauseTick (shouldn't happen but valid safety)
                if (globalTicks >= lastPauseTick)
                {
                    totalPaused += (globalTicks - lastPauseTick);
                }
            }

            return globalTicks - totalPaused;
        }

        public static int GetLocalTicksAbs(Map map)
        {
            if (map == null) return GetWorldTicksAbs();
            // GenTicks.TicksAbs is safer than doing manual calculation if gameStartAbsTick isn't ready
            try 
            {
               return GetLocalTicks(map) + Find.TickManager.gameStartAbsTick;
            }
            catch
            {
               return (Find.TickManager.gameStartAbsTick == 0) ? 0 : Find.TickManager.TicksAbs;
            }
        }

        public static int GetWorldTicksAbs(Map ignoreMap = null)
        {
            var settings = PauseOtherSettlementsSimulation.Settings;
            if (!settings.enableLocalTimeSystem) return (Find.TickManager.gameStartAbsTick == 0) ? 0 : Find.TickManager.TicksAbs;

            // Find the maximum local time among all player maps
            int maxTicks = 0;
            bool found = false;

            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                Map m = maps[i];
                if (m == ignoreMap) continue;

                // Check if this map should contribute to World Time (Max Time)
                // 1. Is it a Player Home?
                // 2. Does it belong to Player Faction?
                // 3. Is it a Pocket Map linked to a Player Settlement?
                bool isPlayerMap = m.IsPlayerHome || m.Parent.Faction == Faction.OfPlayer;

                if (!isPlayerMap && m.Parent is PocketMapParent pmp && pmp.sourceMap != null && pmp.sourceMap.Parent.Faction == Faction.OfPlayer)
                {
                    isPlayerMap = true;
                }
                
                // [Fix] Also include any map where player colonists are present (Speical Regions, Attacks, etc.)
                // These maps effectively become the "Active" player map and should contribute to World Time if they are running.
                if (!isPlayerMap && m.mapPawns.AnyColonistSpawned)
                {
                    isPlayerMap = true;
                }

                if (isPlayerMap)
                {
                    int localAbs = GetLocalTicksAbs(m);
                    if (!found || localAbs > maxTicks)
                    {
                        maxTicks = localAbs;
                        found = true;
                    }
                }
            }

            if (found) return maxTicks;
            return (Find.TickManager.gameStartAbsTick == 0) ? 0 : Find.TickManager.TicksAbs;
        }
    }
}
