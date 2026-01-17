using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PauseOtherSettlementsSimulation.Patches
{
    [HarmonyPatch(typeof(IncidentQueue), "IncidentQueueTick")]
    public static class IncidentQueueTickPatch 
    { 
        [HarmonyPrefix] 
        public static bool Prefix(IncidentQueue __instance)
        {
            if (!PauseOtherSettlementsSimulation.Settings.PauseWeather) return true;

            foreach (QueuedIncident queuedIncident in __instance)
            {
                if (queuedIncident?.FiringIncident?.parms?.target is Map map && SimulationManager.ShouldSimulateMap(map))
                {
                    return true; // If there is an event waiting for a map that is being simulated, run the tick.
                }
            }

            // If there are no events waiting for maps that are being simulated, do not run the tick.
            // However, world (caravan) target events should continue to be processed, so if the queue is not empty, allow the tick.
            // First, we check and control only the map target events.
            // If there are events waiting for non-map targets, they should not run.
            // However, if the queue is not empty, allow the tick.
            // A safer method is to check all the queue and always allow non-map targets.
            bool shouldTick = true;
            foreach (QueuedIncident item in __instance)
            {
                if (item.FiringIncident.parms.target is Map map)
                {
                    if (SimulationManager.ShouldSimulateMap(map))
                    {
                        return true;
                    }
                    shouldTick = false; // If there is an event waiting for a map that is not being simulated, do not run the tick.
                }
                else
                {
                    return true; // Events with non-map targets should always run.
                }
            }

            return shouldTick;
        }
    }

    // This is the final and correct patch to prevent incidents from firing on paused maps.
    // It targets the actual execution method within the IncidentWorker class, which is the last step before an incident occurs.
    [HarmonyPatch(typeof(IncidentWorker), "TryExecute")]
    public static class IncidentWorker_TryExecute_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(IncidentParms parms)
        {
            // If the incident-pausing feature is disabled, always allow execution.
            if (!PauseOtherSettlementsSimulation.Settings.PauseWeather)
            {
                return true;
            }

            // Check if the incident's target is a Map.
            if (parms.target is Map map)
            {
                // If the target map is not being simulated (i.e., paused),
                // block the incident from executing.
                if (!SimulationManager.ShouldSimulateMap(map))
                {
                    return false; // This aborts the original TryExecute method, preventing the incident.
                }
            }
            // Check if the incident's target is a Caravan.
            else if (parms.target is Caravan caravan)
            {
                 if (!CaravanSimulationSystem.ShouldSimulateCaravan(caravan))
                 {
                     return false;
                 }
            }

            // Allow the incident to execute for all other cases (e.g., world targets, caravans, or active maps).
            return true;
        }
    }
}
