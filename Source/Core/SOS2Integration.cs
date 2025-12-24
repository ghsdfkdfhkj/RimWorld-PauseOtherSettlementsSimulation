using System;
using System.Reflection;
using Verse;
using RimWorld;
using HarmonyLib;

namespace PauseOtherSettlementsSimulation
{
    /// <summary>
    /// Handles interaction with Save Our Ship 2 via Reflection to avoid hard dependencies.
    /// </summary>
    public static class SOS2Integration
    {
        private static bool? active = null;
        public static bool IsSOS2Active
        {
            get
            {
                if (active == null)
                {
                    active = ModsConfig.IsActive("kentington.saveourship2");
                }
                return active.Value;
            }
        }

        // Reflection Cache
        private static Type typeShipMapComp;
        private static FieldInfo fieldShipMapState; 
        private static FieldInfo fieldShipCombatOriginMap;
        
        // ShipMapState Enum Values (cached as integers/bytes)
        // 0: nominal, 1: inCombat, 2: isGraveyard, 3: inTransit, 4: inEvent, 5: burnUpSet
        private const int STATE_IN_COMBAT = 1;

        private static void InitReflection()
        {
            if (typeShipMapComp != null) return;
            
            typeShipMapComp = AccessTools.TypeByName("SaveOurShip2.ShipMapComp");
            if (typeShipMapComp != null)
            {
                fieldShipMapState = AccessTools.Field(typeShipMapComp, "ShipMapState");
                fieldShipCombatOriginMap = AccessTools.Field(typeShipMapComp, "ShipCombatOriginMap");
            }
        }

        /// <summary>
        /// Checks if the given map is an SOS2 enemy ship currently in battle with a player ship.
        /// </summary>
        /// <param name="map">The map to check (potential enemy ship)</param>
        /// <param name="playerMap">The player ship map it is fighting against, if any.</param>
        /// <returns>True if this is an enemy ship in combat with a player ship.</returns>
        public static bool IsSOS2BattleMap(Map map, out Map playerMap)
        {
            playerMap = null;
            if (!IsSOS2Active || map == null) return false;
            
            InitReflection();
            if (typeShipMapComp == null) return false;

            // Get component
            // We use GetComponent(Type) extension or just look it up manually.
            // Since we don't have the generic type at compile time:
            var comp = map.GetComponent(typeShipMapComp);
            if (comp == null) return false;

            // Check State == inCombat
            if (fieldShipMapState != null)
            {
                 object stateValue = fieldShipMapState.GetValue(comp);
                 // Enum is byte, so direct cast to int fails. Convert handles it safely.
                 int stateInt = Convert.ToInt32(stateValue);
                 if (stateInt != STATE_IN_COMBAT) return false;
            }

            // Get Origin Map (The player ship map)
            if (fieldShipCombatOriginMap != null)
            {
                 object originMapObj = fieldShipCombatOriginMap.GetValue(comp);
                 if (originMapObj is Map pMap && pMap.Parent.Faction == Faction.OfPlayer)
                 {
                     playerMap = pMap;
                     return true;
                 }
            }

            return false;
        }
    }
}
