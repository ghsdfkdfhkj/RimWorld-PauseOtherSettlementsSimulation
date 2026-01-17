using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PauseOtherSettlementsSimulation
{
    public static class SimulationManager
    {
        private static int simulationCacheTick = -1;
        public static readonly Dictionary<int, bool> mapSimulationCache = new Dictionary<int, bool>();
        private static readonly HashSet<int> mapsCurrentlyPaused = new HashSet<int>();

        public static void UpdateKnownSettlements()
        {
            if (Find.World?.worldObjects?.Settlements == null)
            {
                PauseOtherSettlementsSimulation.Settings.knownSettlements.Clear();
                return;
            }

            var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();

            // Get all current player settlements from the world (surface)
            // Get all current player owned map parents OR maps with player colonists (e.g. SOS2 battles)
            // We only care about things that have a map (or are generating one) and belong to the player.
            // Some mods might use MapParent for camps without them being "Settlement" class.
            var currentPlayerMapParents = Find.World.worldObjects.AllWorldObjects.OfType<MapParent>()
                .Where(mp => mp.HasMap && (mp.Faction == Faction.OfPlayer || (mp.Map != null && mp.Map.mapPawns.AnyColonistSpawned)))
                .ToDictionary(mp => mp.Tile, mp => mp.Label);

            // Remove settlements from our list that no longer exist in the world
            PauseOtherSettlementsSimulation.Settings.knownSettlements.RemoveAll(s => !currentPlayerMapParents.ContainsKey(s.tile));

            // Update names for existing entries and add new ones (surface + off-world)
            foreach (var kv in currentPlayerMapParents)
            {
                int tile = kv.Key;
                string name = kv.Value;
                var existing = PauseOtherSettlementsSimulation.Settings.knownSettlements.FirstOrDefault(s => s.tile == tile);
                if (existing != null)
                {
                    if (existing.name != name)
                    {
                        existing.name = name;
                    }
                }
                else
                {
                    var newInfo = new SettlementInfo { tile = tile, name = name };
                    PauseOtherSettlementsSimulation.Settings.knownSettlements.Add(newInfo);
                    if (!worldComp.settlementPausedStates.ContainsKey(tile))
                    {
                        // Default to running (false). Auto-pause will catch it if needed.
                        worldComp.settlementPausedStates[tile] = false;
                    }
                }
            }
        }

        public static bool ShouldSimulateMap(Map map)
        {
            if (map == null || map == Find.CurrentMap) return true;
            if (!PauseOtherSettlementsSimulation.Settings.PauseOtherSettlements) return true;

            var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();
            bool isPaused;
            if (map.Parent is PocketMapParent pocket)
            {
                // [Sync Feature] If sync is enabled, the pocket map simply mirrors its source map's state.
                // This handles cases where the source map is the CurrentMap (Simulate=true) 
                // or is another simulating map.
                // Sync should only apply if Auto-Pause is generally enabled.
                if (PauseOtherSettlementsSimulation.Settings.autoPauseSettlements && PauseOtherSettlementsSimulation.Settings.enablePocketMapSync && pocket.sourceMap != null)
                {
                    return ShouldSimulateMap(pocket.sourceMap);
                }

                if (worldComp.anomalyMapPausedStates.TryGetValue(map.uniqueID, out bool pausedState))
                {
                    isPaused = pausedState;
                }
                else
                {
                    isPaused = false;
                }
            }
            else if (map.Parent is Settlement settlement)
            {
                isPaused = worldComp.settlementPausedStates.TryGetValue(settlement.Tile, out bool pausedState) ? pausedState : false;
            }
            // Add support for generic MapParents that are player-owned (Camps, etc.)
            else if (map.Parent is MapParent mapParent && mapParent.Faction == Faction.OfPlayer)
            {
                isPaused = worldComp.settlementPausedStates.TryGetValue(mapParent.Tile, out bool pausedState) ? pausedState : false;
            }
            // [SOS2 Support] Support generic maps that have player colonists (e.g. Battles)
            else if (map.mapPawns.AnyColonistSpawned)
            {
                isPaused = worldComp.anomalyMapPausedStates.TryGetValue(map.uniqueID, out bool pausedState) ? pausedState : false;
            }
            else
            {
                // Other maps (space/special maps) are controlled based on map.uniqueID
                isPaused = worldComp.anomalyMapPausedStates.TryGetValue(map.uniqueID, out bool pausedState)
                    ? pausedState
                    : false;
            }
            return !isPaused;
        }

        public static bool ShouldSimulatePawn(Pawn pawn)
        {
            if (pawn == null) return true;
            if (pawn.Map != null) return ShouldSimulateMap(pawn.Map);

            // Handle Caravan Pawns
            var caravan = pawn.GetCaravan();
            if (caravan != null)
            {
                return CaravanSimulationSystem.ShouldSimulateCaravan(caravan);
            }

            return true;
        }

        public static void ApplyMapPauseState(Map map, bool paused)
        {
            if (map == null) return;
            bool already = mapsCurrentlyPaused.Contains(map.uniqueID);
            var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();

            if (paused)
            {
                // Time tracking: Record when pause started if not already recorded
                if (worldComp != null && !worldComp.mapLastPauseTick.ContainsKey(map.uniqueID))
                {
                    worldComp.mapLastPauseTick[map.uniqueID] = Find.TickManager.TicksGame;
                }

                if (!already)
                {
                    // Remove all tickables on this map from tick lists
                    Find.TickManager.RemoveAllFromMap(map);
                    mapsCurrentlyPaused.Add(map.uniqueID);
                }
            }
            else
            {
                // Time tracking: Calculate duration and add to total
                if (worldComp != null && worldComp.mapLastPauseTick.TryGetValue(map.uniqueID, out int lastTick))
                {
                    int duration = Find.TickManager.TicksGame - lastTick;
                    if (duration > 0)
                    {
                        if (!worldComp.mapTotalPausedTicks.ContainsKey(map.uniqueID)) worldComp.mapTotalPausedTicks[map.uniqueID] = 0;
                        worldComp.mapTotalPausedTicks[map.uniqueID] += duration;
                        if (map.uniqueID == 2) Log.Message($"[Debug] ApplyMapPauseState(ID:2) Added duration {duration}, NewTotal: {worldComp.mapTotalPausedTicks[map.uniqueID]}");
                    }
                    worldComp.mapLastPauseTick.Remove(map.uniqueID);
                }

                if (already)
                {
                    // Re-register all things on this map
                    var allThings = map.listerThings.AllThings;
                    for (int i = 0; i < allThings.Count; i++)
                    {
                        Find.TickManager.RegisterAllTickabilityFor(allThings[i]);
                    }
                    mapsCurrentlyPaused.Remove(map.uniqueID);
                }
            }
        }

        public static void SetSettlementPaused(int tileId, bool paused)
        {
            var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();
            bool old = worldComp.settlementPausedStates.TryGetValue(tileId, out var prev) ? prev : false;
            if (old == paused) return;
            worldComp.settlementPausedStates[tileId] = paused;

            // Support any MapParent (e.g., SOS2 Ship, Odyssey Orbit Map), not just "Settlement"
            var mapParent = Find.World.worldObjects.MapParentAt(tileId);
            if (mapParent != null && mapParent.HasMap)
            {
                ApplyMapPauseState(mapParent.Map, paused);
            }
        }

        public static void SetAnomalyMapPaused(int mapUniqueId, bool paused)
        {
            var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();
            bool old = worldComp.anomalyMapPausedStates.TryGetValue(mapUniqueId, out var prev) ? prev : false;
            if (old == paused) return;
            worldComp.anomalyMapPausedStates[mapUniqueId] = paused;
            var map = Find.Maps.FirstOrDefault(m => m.uniqueID == mapUniqueId);
            if (map != null)
            {
                ApplyMapPauseState(map, paused);
            }
        }

        public static void PrecomputeMapSimulationCache()
        {
            int tick = Find.TickManager.TicksGame;
            if (simulationCacheTick == tick) return;
            simulationCacheTick = tick;
            mapSimulationCache.Clear();
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                var m = maps[i];
                bool sim = true;
                try { sim = ShouldSimulateMap(m); }
                catch { sim = true; }
                mapSimulationCache[m.uniqueID] = sim;
            }
        }

        public static bool IsMapSimulatingCached(Map map)
        {
            if (map == null) return true;
            int tick = Find.TickManager.TicksGame;
            if (simulationCacheTick != tick)
            {
                PrecomputeMapSimulationCache();
            }
            if (mapSimulationCache.TryGetValue(map.uniqueID, out bool sim)) return sim;
            return true; // If it's not in the cache, safely allow it
        }
    }
}
