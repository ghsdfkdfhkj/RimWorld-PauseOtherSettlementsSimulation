using HarmonyLib;
using Verse;
using RimWorld;

namespace PauseOtherSettlementsSimulation
{
    [StaticConstructorOnStartup]
    public static class ModStartup
    {
        static ModStartup()
        {
            Log.Message("[PauseOtherSettlementsSimulation] Initializing Harmony Patches...");
            var harmony = new Harmony("YourName.PauseOtherSettlementsSimulation");
            harmony.PatchAll();
            Log.Message("[PauseOtherSettlementsSimulation] Harmony Patches Initialized.");
        }
    }
}
