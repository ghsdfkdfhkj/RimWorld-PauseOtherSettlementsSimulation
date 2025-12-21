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

            if (!PauseOtherSettlementsSimulation.Settings.pauseAnomalyLayersWhenAway) return;

            // 모든 변칙 레이어를 순회합니다.
            foreach (var map in Find.Maps)
            {
                if (map.Parent is PocketMapParent pocketMapParent)
                {
                    // 변칙 레이어의 부모 정착지를 찾습니다.
                    Map sourceMap = pocketMapParent.sourceMap;
                    Settlement parentSettlement = sourceMap?.Parent as Settlement;

                    // 부모 정착지가 없거나 플레이어의 소유가 아니면 건너뜁니다.
                    if (parentSettlement == null || parentSettlement.Faction != Faction.OfPlayer)
                    {
                        // 부모를 찾을 수 없는 경우, 플레이어가 맵 내부에 있을 때만 시뮬레이션합니다.
                        anomalyMapPausedStates[map.uniqueID] = Find.CurrentMap != map;
                        continue;
                    }

                    bool isAway = true; // 기본적으로 자리를 비운 것으로 가정합니다.

                    // Case 1: 플레이어가 변칙 레이어 맵 자체를 보고 있는 경우
                    if (Find.CurrentMap == map)
                    {
                        isAway = false;
                    }
                    // Case 2: 플레이어가 부모 정착지를 보고 있는 경우
                    else if (Find.CurrentMap?.Tile == parentSettlement.Tile)
                    {
                        isAway = false;
                    }
                    
                    bool oldState = false;
                    if (anomalyMapPausedStates.TryGetValue(map.uniqueID, out bool val)) oldState = val;
                    
                    if (oldState != isAway)
                    {
                         anomalyMapPausedStates[map.uniqueID] = isAway;
                         // Also trigger state application immediately for this map
                         PauseOtherSettlementsSimulation.ApplyMapPauseState(map, isAway);
                    }
                }
            }
        }
    }
}
