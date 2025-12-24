using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace PauseOtherSettlementsSimulation
{
    /// <summary>
    /// Handles simulation state for Caravans.
    /// User requested this to be in a separate file.
    /// </summary>
    public static class CaravanSimulationSystem
    {
        public static bool ShouldSimulateCaravan(Caravan caravan)
        {
            if (caravan == null || !PauseOtherSettlementsSimulation.Settings.PauseOtherSettlements) return true;

            var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();
            if (worldComp == null) return true;

            if (worldComp.caravanPausedStates.TryGetValue(caravan.ID, out bool paused))
            {
                return !paused;
            }

            return true;
        }

        public static void SetCaravanPaused(int caravanId, bool paused)
        {
            var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();
            bool old = worldComp.caravanPausedStates.TryGetValue(caravanId, out var prev) ? prev : false;
            if (old == paused) return;

            worldComp.caravanPausedStates[caravanId] = paused;
        }
    }

    [HarmonyPatch(typeof(WorldObject), "Tick")]
    public static class WorldObject_Tick_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(WorldObject __instance)
        {
            if (__instance is Caravan caravan)
            {
                return CaravanSimulationSystem.ShouldSimulateCaravan(caravan);
            }
            return true;
        }
    }



    [HarmonyPatch(typeof(Caravan), "NightResting", MethodType.Getter)]
    public static class Caravan_NightResting_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Caravan __instance, ref bool __result)
        {
            // If paused, force the caravan to think it's "Night Resting".
            // This stops movement and food consumption in the vanilla PatherTick.
            if (!CaravanSimulationSystem.ShouldSimulateCaravan(__instance))
            {
                __result = true;
            }
        }
    }
}
