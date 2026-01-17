using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace PauseOtherSettlementsSimulation.Patches
{
    [HarmonyPatch(typeof(TickList), "Tick")]
    public static class TickList_Tick_Transpiler
    {
        private static readonly MethodInfo miDoTick = AccessTools.Method(typeof(Thing), nameof(Thing.DoTick));
        private static readonly MethodInfo miShould = AccessTools.Method(typeof(SimulationManager), nameof(SimulationManager.IsMapSimulatingCached));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(miDoTick))
                {
                    // before callvirt Thing.DoTick()
                    Label afterCall = il.DefineLabel();
                    // Attach a label to the next command
                    int nextIndex = i + 1;
                    if (nextIndex < codes.Count)
                    {
                        codes[nextIndex].labels.Add(afterCall);
                    }
                    // Insert:
                    // dup
                    // callvirt instance Map Verse.Thing::get_Map()
                    // call bool SimulationManager.IsMapSimulatingCached(Map)
                    // brfalse.s afterCall
                    var injected = new List<CodeInstruction>
                    {
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map))),
                        new CodeInstruction(OpCodes.Call, miShould),
                        new CodeInstruction(OpCodes.Brfalse_S, afterCall)
                    };
                    codes.InsertRange(i, injected);
                    i += injected.Count; // skip
                }
            }
            return codes;
        }
    }

    [HarmonyPatch(typeof(TickManager), "DoSingleTick")]
    public static class TickManager_DoSingleTick_CachePatch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            SimulationManager.PrecomputeMapSimulationCache();
        }
    }

    [HarmonyPatch(typeof(Map), "MapPostTick")]
    public static class MapPostTickPatch { [HarmonyPrefix] public static bool Prefix(Map __instance) => !PauseOtherSettlementsSimulation.Settings.PauseWeather || SimulationManager.ShouldSimulateMap(__instance); }

    [HarmonyPatch(typeof(Storyteller), "StorytellerTick")]
    public static class StorytellerTickPatch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            // Storyteller a always ticking is safer to prevent unexpected time jump bugs.
            // The IncidentQueueTickPatch will handle preventing incidents on paused maps.
            return true;
        }
    }
}
