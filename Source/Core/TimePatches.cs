using HarmonyLib;
using RimWorld;
using Verse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using PauseOtherSettlementsSimulation;
using RimWorld.Planet;

namespace PauseOtherSettlementsSimulation.Patches
{
    [HarmonyPatch(typeof(GenCelestial), "CurCelestialSunGlow")]
    public static class GenCelestial_CurCelestialSunGlow_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var tickManagerProp = AccessTools.Property(typeof(Find), nameof(Find.TickManager)).GetGetMethod();
            var ticksAbsProp = AccessTools.Property(typeof(TickManager), nameof(TickManager.TicksAbs)).GetGetMethod();
            var getLocalMethod = AccessTools.Method(typeof(LocalTimeManager), nameof(LocalTimeManager.GetLocalTicksAbs));

            bool found = false;
            for (int i = 0; i < codes.Count; i++)
            {
                // Look for: Find.TickManager.TicksAbs
                if (codes[i].Calls(ticksAbsProp))
                {
                    if (i > 0 && codes[i - 1].Calls(tickManagerProp))
                    {
                        // Replace 'call Find.TickManager' with 'ldarg.0' (Map map)
                        // Preserve labels by modifying the existing instruction
                        codes[i - 1].opcode = OpCodes.Ldarg_0;
                        codes[i - 1].operand = null;
                        
                        // Replace 'callvirt TicksAbs' with 'call LocalTimeManager.GetLocalTicksAbs(Map)'
                        codes[i].opcode = OpCodes.Call;
                        codes[i].operand = getLocalMethod;
                        
                        found = true;
                    }
                }
            }

            if (!found)
            {
                Log.Warning("[PauseOtherSettlementsSimulation] GenCelestial transpiler failed to find target instructions.");
            }

            return codes.AsEnumerable();
        }
    }

    [HarmonyPatch(typeof(GenTemperature), "GetTemperatureFromSeasonAtTile")]
    public static class GenTemperature_GetTemperatureFromSeasonAt_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(ref int __0, object __1)
        {
            // Respect the setting!
            if (!PauseOtherSettlementsSimulation.Settings.enableLocalTimeSystem) return;

            ref int absTick = ref __0;
            object tileObj = __1;
            int tile = -1;

            if (tileObj is int i)
            {
                tile = i;
            }
            else if (tileObj != null)
            {
                // Handle "PlanetTile" or other wrapper structs via reflection
                // Try "tile" field first, then implicit cast, or just ToString() as last resort?
                // Assuming it has a field named "tile" or similar.
                try 
                {
                    var field = tileObj.GetType().GetField("tile");
                    if (field != null && field.FieldType == typeof(int))
                    {
                        tile = (int)field.GetValue(tileObj);
                    }
                    else
                    {
                        // Fallback: Check for any public int field? Or maybe properties?
                        // This corresponds to RimWorld.Planet.PlanetTile if it exists. 
                        // If it's the one from some mods, it likely wraps the int.
                         var prop = tileObj.GetType().GetProperty("Tile");
                         if (prop != null && prop.PropertyType == typeof(int))
                         {
                             tile = (int)prop.GetValue(tileObj);
                         }
                    }
                }
                catch (Exception)
                {
                    // If reflection fails, we can't do anything safe. 
                    // Just return and let original method run.
                    return;
                }
            }

            if (tile == -1) return;

            // If the query is likely for "Current Time" (close to global TicksAbs),
            // and we have a map for this tile, adjust the tick to Local Time.
            // This ensures seasonal temperatures match the local season.
            
            int globalNow = 0;
            if (Find.TickManager.gameStartAbsTick != 0)
            {
                globalNow = Find.TickManager.TicksAbs;
            }
            if (Math.Abs(globalNow - absTick) < 5000)
            {
                Map map = Current.Game.Maps.FirstOrDefault(m => m.Tile == tile);
                if (map != null)
                {
                    // Calculate the offset based on how long this map has been paused.
                    // LocalAbs = GlobalAbs - TotalPaused
                    // We modify the 'absTick' argument directly.
                    
                    int totalPaused = 0;
                    var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();
                    if (worldComp != null)
                    {
                        if (worldComp.mapTotalPausedTicks.TryGetValue(map.uniqueID, out int p)) 
                            totalPaused += p;
                        
                        // Add current pause duration if active
                        // Note: Using TicksGame for pause verification, which is safe relative to TicksAbs
                        int ticksGame = Find.TickManager.TicksGame;
                        if (worldComp.mapLastPauseTick.TryGetValue(map.uniqueID, out int l) && l >= 0 && l <= ticksGame)
                        {
                            totalPaused += (ticksGame - l);
                        }
                    }
                    
                    absTick -= totalPaused;
                }
                else
                {
                    // [Global Time Sync]
                    // If asking for a Tile without a Map (World map tile), use the effective World Time
                    // which is synced to the fastest settlement.
                    absTick = LocalTimeManager.GetWorldTicksAbs();
                }
            }
        }
    }

    [HarmonyPatch(typeof(DateReadout), "DateOnGUI")]
    public static class DateReadout_DateOnGUI_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var tickManagerProp = AccessTools.PropertyGetter(typeof(Find), nameof(Find.TickManager));
            var ticksAbsProp = AccessTools.PropertyGetter(typeof(TickManager), nameof(TickManager.TicksAbs));
            
            var currentMapProp = AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));
            var getLocalMethod = AccessTools.Method(typeof(LocalTimeManager), nameof(LocalTimeManager.GetLocalTicksAbs));

            bool found = false;
            for (int i = 0; i < codes.Count; i++)
            {
                // We are looking for the sequence:
                // call Verse.Find::get_TickManager()
                // callvirt Verse.TickManager::get_TicksAbs()
                
                if (codes[i].Calls(ticksAbsProp))
                {
                    if (i > 0 && codes[i - 1].Calls(tickManagerProp))
                    {
                        // Replace 'call Find.TickManager' with 'call Find.CurrentMap'
                        // Preserve labels!
                        codes[i - 1].opcode = OpCodes.Call;
                        codes[i - 1].operand = currentMapProp;
                        
                        // Replace 'callvirt TicksAbs' with 'call LocalTimeManager.GetLocalTicksAbs(Map)'
                        codes[i].opcode = OpCodes.Call;
                        codes[i].operand = getLocalMethod;
                        
                        found = true;
                    }
                }
            }
            
            if (!found)
            {
                Log.Warning("[PauseOtherSettlementsSimulation] DateReadout_DateOnGUI_Patch could not find TicksAbs to patch.");
            }
            
            return codes;
        }
    }

    [HarmonyPatch(typeof(StorytellerUtility), "DefaultThreatPointsNow")]
    public static class StorytellerUtility_DefaultThreatPointsNow_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            
            // StorytellerUtility.DefaultThreatPointsNow(IIncidentTarget target)
            // We want to replace TicksGame or TicksAbs usage with LocalTimeManager.GetLocalTicks(Map)
            // But we need 'Map'. 'target' is IIncidentTarget.
            // target as Map ?? (target as MapParent)?.Map ?? ...
            
            var tickManagerProp = AccessTools.PropertyGetter(typeof(Find), nameof(Find.TickManager));
            var ticksGameProp = AccessTools.PropertyGetter(typeof(TickManager), nameof(TickManager.TicksGame));
            var ticksAbsProp = AccessTools.PropertyGetter(typeof(TickManager), nameof(TickManager.TicksAbs));
            
            var getLocalTicksMethod = AccessTools.Method(typeof(StorytellerUtility_DefaultThreatPointsNow_Patch), nameof(GetLocalTicksForTarget));

            for (int i = 0; i < codes.Count; i++)
            {
                // Looking for TicksGame usually used for "Time Points" calculation
                if (codes[i].Calls(ticksGameProp) || codes[i].Calls(ticksAbsProp))
                {
                    if (i > 0 && codes[i - 1].Calls(tickManagerProp))
                    {
                        // Replace 'call Find.TickManager' with 'ldarg.0' (IIncidentTarget)
                        codes[i - 1].opcode = OpCodes.Ldarg_0;
                        codes[i - 1].operand = null;
                        
                        // Replace 'callvirt TicksGame' with 'call GetLocalTicksForTarget(IIncidentTarget)'
                        codes[i].opcode = OpCodes.Call;
                        codes[i].operand = getLocalTicksMethod;
                        
                        // found = true; not needed
                    }
                }
            }
            
            return codes;
        }

        public static int GetLocalTicksForTarget(IIncidentTarget target)
        {
            Map map = target as Map;
            if (map == null && target is MapParent mp) map = mp.Map;
            
            if (map != null)
            {
                return LocalTimeManager.GetLocalTicks(map);
            }
            
            return Find.TickManager.TicksGame;
        }
    }

    // [REMOVED] Letter_GetMouseoverText_Patch caused crash because GetMouseoverText is abstract.
    // We would need to patch ChoiceLetter or other concrete classes instead.
    // For now, removing to fix crash.
}
