using HarmonyLib;
using RimWorld;
using System;

namespace PauseOtherSettlementsSimulation.Patches
{
    // Fix for VEF / General "InvalidCastException" in MemoryThoughtHandler reported by user
    [HarmonyPatch(typeof(Thought_MemorySocialCumulative), "TryMergeWithExistingMemory")]
    public static class Thought_MemorySocialCumulative_TryMergeWithExistingMemory_Patch
    {
        [HarmonyFinalizer]
        public static Exception Finalizer(Exception __exception, ref bool showBubble)
        {
            if (__exception is InvalidCastException)
            {
                // Suppress the exception to prevent error spam (harmless according to user reports)
                // This seems to happen when VEF is active and maps are paused/unpaused, leading to some Thought type mismatch?
                showBubble = false;
                return null;
            }
            return __exception;
        }
    }
}
