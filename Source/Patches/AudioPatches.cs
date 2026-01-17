using HarmonyLib;
using RimWorld;
using System.Reflection;
using Verse;
using Verse.Sound;

namespace PauseOtherSettlementsSimulation.Patches
{
    // This is the primary, robust patch that prevents the sustainer error at its source.
    [HarmonyPatch(typeof(Sustainer), "Maintain")]
    public static class Sustainer_Maintain_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Sustainer __instance)
        {
            // This patch specifically targets the "FleshmassAmbience" sustainer error.
            if (__instance?.def == SoundDefOf.FleshmassAmbience)
            {
                // Case 1: The sustainer has already been ended for any reason.
                // The original method's first action is to log an error if this is true.
                // We prevent that error by simply stopping the method here.
                if (__instance.Ended)
                {
                    return false; // Abort original method.
                }

                // Case 2: The map containing the fleshmass is now paused by our mod.
                // We need to end the sustainer and prevent the original method from running.
                if (__instance.info.Maker.HasThing && 
                    __instance.info.Maker.Thing?.Map != null &&
                    !SimulationManager.IsMapSimulatingCached(__instance.info.Maker.Thing.Map))
                {
                    __instance.End();
                    return false; // Abort original method.
                }
            }

            // For all other sustainers, or for FleshmassAmbience on a simulating map, run the original method.
            return true;
        }
    }

    [HarmonyPatch(typeof(Building_MusicalInstrument), "Tick")]
    public static class Building_MusicalInstrument_Tick_Patch
    {
        private static FieldInfo soundPlayingField = AccessTools.Field(typeof(Building_MusicalInstrument), "soundPlaying");

        [HarmonyPrefix]
        public static void Prefix(Building_MusicalInstrument __instance)
        {
            // When the map is paused and then resumed, the existing soundPlaying Sustainer may already be dead.
            // Maintaining a dead Sustainer will cause an error, so we check and initialize it to null in advance.
            // This will cause the original Tick to create a new Sustainer.
            if (soundPlayingField != null)
            {
                Sustainer soundPlaying = (Sustainer)soundPlayingField.GetValue(__instance);
                if (soundPlaying != null && soundPlaying.Ended)
                {
                    soundPlayingField.SetValue(__instance, null);
                }
            }
        }
    }
}
