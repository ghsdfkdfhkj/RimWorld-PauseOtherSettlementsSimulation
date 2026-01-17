using HarmonyLib;
using Verse;
using System.Reflection;

namespace PauseOtherSettlementsSimulation.Patches
{
    [HarmonyPatch(typeof(RoomTempTracker), "EqualizeTemperature")]
    public static class RoomTempTracker_EqualizeTemperature_Patch
    {
        private static FieldInfo roomField = AccessTools.Field(typeof(RoomTempTracker), "room");

        [HarmonyPrefix]
        public static bool Prefix(RoomTempTracker __instance)
        {
            var room = (Room)roomField.GetValue(__instance);
            if (room == null || room.Map == null) return true;

            return SimulationManager.ShouldSimulateMap(room.Map);
        }
    }
}
