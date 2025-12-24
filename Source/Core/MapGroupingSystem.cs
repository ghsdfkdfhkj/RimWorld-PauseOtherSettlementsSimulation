using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PauseOtherSettlementsSimulation
{
    public static class MapGroupingSystem
    {
        public static Dictionary<int, List<Map>> GetGroupedMaps()
        {
            var groupedMaps = new Dictionary<int, List<Map>>();

            foreach (var map in Find.Maps)
            {
                // 1. Packet Maps linked to a valid source map (Vanilla Anomaly, etc.)
                if (map.Parent is PocketMapParent pocketMapParent && pocketMapParent.sourceMap != null)
                {
                    // Generally link to the source map's parent tile
                    MapParent sourceParent = pocketMapParent.sourceMap.Parent;
                    
                    if (sourceParent != null && sourceParent.Faction == Faction.OfPlayer)
                    {
                        AddToGroup(groupedMaps, sourceParent.Tile, map);
                        continue;
                    }
                }

                // 2. SOS2 Battle Maps / Other Temporary Maps
                // Check if this is a "Main" player map (Settlement, Ship, etc.)
                // If it is, we skip it here because it will be a Header in the UI.
                bool isMainPlayerMap = map.Parent is MapParent mp && mp.Faction == Faction.OfPlayer;
                
                // Exception: If a main player map is somehow a Pocket Map (unlikely for Settlement, but possible for mods), treating it as child might be desired.
                // But generally IsMainPlayerMap means "Header".

                if (isMainPlayerMap) 
                {
                    continue; 
                }

                // 2. SOS2 Battle Maps (Deep Integration)
                if (SOS2Integration.IsSOS2BattleMap(map, out Map playerShipMap))
                {
                     AddToGroup(groupedMaps, playerShipMap.Parent.Tile, map);
                     continue;
                }

                // 3. Fallback: Other Temporary Maps (Ambush, etc.)
                // Maps that are active but not caught by above checks.
                if (map.mapPawns.AnyColonistSpawned)
                {
                    // Check for a "Parent" object at this same tile.
                    // This handles generic battles happening at the same tile as a Player Settlement/Ship.
                    var distinctPlayerParent = Find.World.worldObjects.AllWorldObjects
                        .OfType<MapParent>()
                        .FirstOrDefault(x => x.Tile == map.Tile && x.Faction == Faction.OfPlayer && x.HasMap && x.Map != map);

                    if (distinctPlayerParent != null)
                    {
                         AddToGroup(groupedMaps, distinctPlayerParent.Tile, map);
                         continue;
                    }
                }

                // 4. Independent / Special Maps
                // Maps that are active (checked by AnyColonistSpawned or IsPlayerHome) but have no parent settlement nearby.
                // e.g. Far away ambush, Caravan ambush.
                if (map.mapPawns.AnyColonistSpawned || map.IsPlayerHome)
                {
                    AddToGroup(groupedMaps, -map.uniqueID, map);
                }
            }

            return groupedMaps;
        }

        private static void AddToGroup(Dictionary<int, List<Map>> dict, int key, Map map)
        {
            if (!dict.ContainsKey(key))
            {
                dict[key] = new List<Map>();
            }
            dict[key].Add(map);
        }
    }
}
