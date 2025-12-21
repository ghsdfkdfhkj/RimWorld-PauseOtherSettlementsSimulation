using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PauseOtherSettlementsSimulation
{
    // This component is attached to the game's World object and is saved with the game.
    // It holds the custom names for anomaly maps for a specific save file.
    public class CustomNameWorldComponent : WorldComponent
    {
        // Key: Map unique ID, Value: Custom Name
        private Dictionary<int, string> anomalyMapCustomNames = new Dictionary<int, string>();
        public Dictionary<int, bool> settlementPausedStates = new Dictionary<int, bool>();
        public Dictionary<int, bool> anomalyMapPausedStates = new Dictionary<int, bool>();

        // Key: Map unique ID, Value: Total ticks this map has been paused
        public Dictionary<int, int> mapTotalPausedTicks = new Dictionary<int, int>();
        // Key: Map unique ID, Value: The global tick when the map was last paused
        public Dictionary<int, int> mapLastPauseTick = new Dictionary<int, int>();

        public CustomNameWorldComponent(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            // This ensures the dictionary is saved and loaded with the game.
            Scribe_Collections.Look(ref anomalyMapCustomNames, "anomalyMapCustomNames", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref settlementPausedStates, "settlementPausedStates", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref anomalyMapPausedStates, "anomalyMapPausedStates", LookMode.Value, LookMode.Value);
            
            Scribe_Collections.Look(ref mapTotalPausedTicks, "mapTotalPausedTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref mapLastPauseTick, "mapLastPauseTick", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                anomalyMapCustomNames ??= new Dictionary<int, string>();
                settlementPausedStates ??= new Dictionary<int, bool>();
                anomalyMapPausedStates ??= new Dictionary<int, bool>();
                mapTotalPausedTicks ??= new Dictionary<int, int>();
                mapLastPauseTick ??= new Dictionary<int, int>();
            }
        }

        public string GetCustomName(Map map)
        {
            if (anomalyMapCustomNames.TryGetValue(map.uniqueID, out string customName))
            {
                return customName;
            }
            return map.Parent.def.label; // Return default name if no custom name is set
        }

        public void SetCustomName(Map map, string name)
        {
            anomalyMapCustomNames[map.uniqueID] = name;
        }

        // Track the last active map to detect switches
        private Map lastCurrentMap = null;

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            // Detect Map Switch
            Map currentMap = Find.CurrentMap;
            if (currentMap != lastCurrentMap)
            {
                // Map has changed (or game loaded). Update pause states for ALL maps.
                var maps = Find.Maps;
                for (int i = 0; i < maps.Count; i++)
                {
                    Map m = maps[i];
                    bool shouldSim = PauseOtherSettlementsSimulation.ShouldSimulateMap(m);
                    // If shouldSim is true, pause is false.
                    // If shouldSim is false, pause is true.
                    PauseOtherSettlementsSimulation.ApplyMapPauseState(m, !shouldSim);
                }
                lastCurrentMap = currentMap;
            }

            if (Find.TickManager.TicksGame % 60 != 0) return;

            var settings = LoadedModManager.GetMod<PauseOtherSettlementsSimulation>().GetSettings<PauseOtherSettlementsSimulationSettings>();
            if (settings == null) return;

            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome && map.Parent.Faction != Faction.OfPlayer) continue;

                // Determine if this map is subject to auto-pause
                bool isTargetForAutoPause = false;
                bool isAway = (Find.CurrentMap != map);

                if (map.Parent is PocketMapParent pocketMapParent)
                {
                    // User Request: "Auto-pause pocket maps is a sub-setting of auto-pause settlements"
                    // So we only check pocket maps if settings.autoPauseSettlements is TRUE.
                    if (settings.autoPauseSettlements && settings.autoPausePocketMaps)
                    {
                        isTargetForAutoPause = true;
                        // Special rule: If player is in the source map (parent settlement), we are not "away" effectively.
                        // "Together" means: Parent Active <-> Child Active.
                        if (pocketMapParent.sourceMap != null && Find.CurrentMap == pocketMapParent.sourceMap)
                        {
                            isAway = false;
                        }
                    }
                }
                else if (map.Parent is Settlement || map.Parent is MapParent) // Generic catch-all for other player maps (SOS2 ships etc)
                {
                     if (settings.autoPauseSettlements)
                     {
                         isTargetForAutoPause = true;
                         
                         // Check if we are currently viewing a child Pocket Map of this settlement.
                         // If so, we are effectively "at" the settlement (Synced behavior).
                         if (isAway) // Only check if we are physically away from the settlement itself
                         {
                             // Iterate maps to find if current map is a child of 'map'
                             Map current = Find.CurrentMap;
                             if (current != null && current.Parent is PocketMapParent pmp && pmp.sourceMap == map)
                             {
                                 isAway = false;
                             }
                         }
                     }
                }

                if (isTargetForAutoPause)
                {
                    bool currentState = false;
                    bool isPocket = map.Parent is PocketMapParent;
                    
                    if (isPocket)
                    {
                         if (anomalyMapPausedStates.TryGetValue(map.uniqueID, out bool val)) currentState = val;
                    }
                    else
                    {
                         if (settlementPausedStates.TryGetValue(map.Tile, out bool val)) currentState = val;
                    }

                    // Only update if different.
                    // If isAway is TRUE, we want to Pause (true).
                    // If isAway is FALSE, we want to Unpause (false) -> OR do we?
                    // Auto-Pause usually implies "Auto-Resume" when you return.
                    // So yes, we enforce 'isAway' as the pause state.

                    if (currentState != isAway)
                    {
                        if (isPocket) anomalyMapPausedStates[map.uniqueID] = isAway;
                        else settlementPausedStates[map.Tile] = isAway;

                        PauseOtherSettlementsSimulation.ApplyMapPauseState(map, isAway);
                    }
                }
            }
        }
    }
}
