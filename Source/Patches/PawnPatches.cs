using HarmonyLib;
using RimWorld;
using System.Reflection;
using Verse;
using Verse.AI;

namespace PauseOtherSettlementsSimulation.Patches
{
    [HarmonyPatch(typeof(Pawn_AgeTracker), "AgeTickInterval")]
    public static class Pawn_AgeTracker_AgeTickInterval_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn)
        {
            if (!PauseOtherSettlementsSimulation.Settings.PauseAgeing) return true;
            return SimulationManager.ShouldSimulatePawn(___pawn);
        }
    }

    [HarmonyPatch(typeof(Gene_Hemogen), "TickInterval")]
    public static class Gene_Hemogen_TickInterval_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Gene_Hemogen __instance)
        {
            return SimulationManager.ShouldSimulatePawn(__instance.pawn);
        }
    }

    [HarmonyPatch(typeof(JobDriver), "DriverTick")]
    public static class JobDriverTickPatch { [HarmonyPrefix] public static bool Prefix(JobDriver __instance) => !PauseOtherSettlementsSimulation.Settings.PauseOngoingJobs || __instance.pawn == null || SimulationManager.ShouldSimulatePawn(__instance.pawn); }

    [HarmonyPatch(typeof(JobDriver), "DriverTickInterval")]
    public static class JobDriverTickIntervalPatch { [HarmonyPrefix] public static bool Prefix(JobDriver __instance) => !PauseOtherSettlementsSimulation.Settings.PauseOngoingJobs || __instance.pawn == null || SimulationManager.ShouldSimulatePawn(__instance.pawn); }

    [HarmonyPatch(typeof(Pawn_TrainingTracker), "TrainingTrackerTickRare")]
    public static class Pawn_TrainingTracker_TrainingTrackerTickRare_Patch
    {
        private static FieldInfo countDecayFromField = AccessTools.Field(typeof(Pawn_TrainingTracker), "countDecayFrom");

        [HarmonyPrefix]
        public static bool Prefix(Pawn_TrainingTracker __instance, Pawn ___pawn)
        {
            if (SimulationManager.ShouldSimulatePawn(___pawn)) return true;

            // While the map is paused, only the training decay timer will run (add 250 ticks).
            // This protects against sudden decay when the map is reloaded.
            // This is the same logic as when the pawn is Suspended.
            if (countDecayFromField != null)
            {
                int current = (int)countDecayFromField.GetValue(__instance);
                countDecayFromField.SetValue(__instance, current + 250);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_LearningTracker), "LearningTickInterval")]
    public static class Pawn_LearningTracker_LearningTickInterval_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn)
        {
            return SimulationManager.ShouldSimulatePawn(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_NeedsTracker), "NeedsTrackerTickInterval")]
    public static class NeedsTrackerTickIntervalPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn)
        {
            return SimulationManager.ShouldSimulatePawn(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "HealthTick")]
    public static class HealthTickPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn)
        {
            if (!PauseOtherSettlementsSimulation.Settings.PauseHealth) return true;
            return SimulationManager.ShouldSimulatePawn(___pawn);
        }
    }
    
    [HarmonyPatch(typeof(Pawn_HealthTracker), "HealthTickInterval")]
    public static class HealthTickIntervalPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn)
        {
            if (!PauseOtherSettlementsSimulation.Settings.PauseHealth) return true;
            return SimulationManager.ShouldSimulatePawn(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_MindState), "MindStateTickInterval")]
    public static class Pawn_MindState_MindStateTickInterval_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn_MindState __instance)
        {
            if (!PauseOtherSettlementsSimulation.Settings.PauseMentalState) return true;
            return SimulationManager.ShouldSimulatePawn(__instance.pawn);
        }
    }
}
