using HarmonyLib;
using RimWorld;
using Verse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using PauseOtherSettlementsSimulation;

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
        public static void Prefix(int __0, ref int __1)
        {
            int tile = __0;
            ref int absTick = ref __1;

            // If the query is likely for "Current Time" (close to global TicksAbs),
            // and we have a map for this tile, adjust the tick to Local Time.
            // This ensures seasonal temperatures match the local season.
            
            int globalNow = Find.TickManager.TicksAbs;
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
}
