using Verse;
using RimWorld;
using RimWorld.Planet;

namespace PauseOtherSettlementsSimulation
{
    public static class LocalTimeManager
    {
        public static int GetLocalTicks(Map map)
        {
            var settings = LoadedModManager.GetMod<PauseOtherSettlementsSimulation>().GetSettings<PauseOtherSettlementsSimulationSettings>();
            if (!settings.enableLocalTimeSystem) return Find.TickManager.TicksGame;

            if (map == null) return Find.TickManager.TicksGame;

            var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();
            if (worldComp == null) return Find.TickManager.TicksGame;

            int globalTicks = Find.TickManager.TicksGame;
            int totalPaused = 0;

            if (worldComp.mapTotalPausedTicks.TryGetValue(map.uniqueID, out int storedPaused))
            {
                totalPaused += storedPaused;
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
            return GetLocalTicks(map) + Find.TickManager.gameStartAbsTick;
        }
    }
}
