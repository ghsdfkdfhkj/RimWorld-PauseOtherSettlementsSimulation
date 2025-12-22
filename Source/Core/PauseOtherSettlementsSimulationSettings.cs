using Verse;
using UnityEngine;
using System.Collections.Generic;
using RimWorld.Planet; // Needed for SettlementInfo
using System.Linq;

namespace PauseOtherSettlementsSimulation
{
    [StaticConstructorOnStartup]
    public static class Compatibility_VehicleFramework
    {
        public static readonly bool VehicleFrameworkActive;

        static Compatibility_VehicleFramework()
        {
            VehicleFrameworkActive = ModLister.GetActiveModWithIdentifier("Vehicles") != null;
        }
    }
    public class SettlementInfo : IExposable
    {
        public int tile;
        public string name;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tile, "tile");
            Scribe_Values.Look(ref name, "name");
        }
    }

    public class PauseOtherSettlementsSimulationSettings : ModSettings
    {
        // Core settings
        public bool PauseOtherSettlements = true;
        public bool PauseOngoingJobs = true;
        public bool PauseHealth = true;
        public bool PauseWeather = true;
        public bool PauseMentalState = true;
        public bool PauseAgeing = true;
        public bool enableVehicleFrameworkPatch = true; // VF Patch Toggle

        // New/Renamed settings
        public bool autoPausePocketMaps = true; // Renamed from pauseAnomalyLayersWhenAway
        public bool autoPauseSettlements = true; // New (Default: True)
        public bool enablePocketMapSync = false; // New: Controls "Shared Presence" logic
        public bool enableLocalTimeSystem = false; // New: Controls Local Time feature & UI

        // Dictionary to store expanded states of settlements in the UI
        public Dictionary<int, bool> settlementExpandedStates = new Dictionary<int, bool>();

        // Language override setting
        public string manualLanguageOverride = "Auto"; 
        public List<SettlementInfo> knownSettlements = new List<SettlementInfo>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref PauseOtherSettlements, "pauseOtherSettlements", true);
            Scribe_Values.Look(ref PauseOngoingJobs, "pauseOngoingJobs", true);
            Scribe_Values.Look(ref PauseHealth, "pauseHealth", true);
            Scribe_Values.Look(ref PauseWeather, "pauseWeather", true);
            Scribe_Values.Look(ref PauseMentalState, "pauseMentalState", true);
            Scribe_Values.Look(ref PauseAgeing, "pauseAgeing", true);
            Scribe_Values.Look(ref enableVehicleFrameworkPatch, "enableVehicleFrameworkPatch", true);
            
            // Try to load with new name, fallback to old name if needed (optional, but good practice)
            Scribe_Values.Look(ref autoPausePocketMaps, "autoPausePocketMaps", true);
            if (Scribe.mode == LoadSaveMode.LoadingVars && !autoPausePocketMaps)
            {
                // Attempt legacy load
                bool legacyVal = true;
                Scribe_Values.Look(ref legacyVal, "pauseAnomalyLayersWhenAway", true);
                if (!legacyVal) autoPausePocketMaps = false; // Sync if old value was false
            }

            Scribe_Values.Look(ref autoPauseSettlements, "autoPauseSettlements", true);
            Scribe_Values.Look(ref enablePocketMapSync, "enablePocketMapSync", true);
            Scribe_Values.Look(ref enableLocalTimeSystem, "enableLocalTimeSystem", true);

            Scribe_Values.Look(ref manualLanguageOverride, "manualLanguageOverride", "Auto");
            
            Scribe_Collections.Look(ref settlementExpandedStates, "settlementExpandedStates", LookMode.Value, LookMode.Value);
            if (settlementExpandedStates == null)
            {
                settlementExpandedStates = new Dictionary<int, bool>();
            }
            Scribe_Collections.Look(ref knownSettlements, "knownSettlements", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                settlementExpandedStates ??= new Dictionary<int, bool>();
                knownSettlements ??= new List<SettlementInfo>();
            }
        }

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            // Global Enable
            listingStandard.CheckboxLabeled("PauseOtherSettlementsSimulation".Translate(), ref PauseOtherSettlements, "PauseOtherSettlements_Tooltip".Translate());
            listingStandard.GapLine();

            // Local Time System
            listingStandard.CheckboxLabeled("PauseTab_EnableLocalTimeSystem".Translate(), ref enableLocalTimeSystem, "PauseTab_EnableLocalTimeSystemTooltip".Translate());
            listingStandard.Gap();

            // Auto-Pause Settings
            listingStandard.CheckboxLabeled("PauseTab_AutoPauseSettlements".Translate(), ref autoPauseSettlements, "PauseTab_AutoPauseSettlementsTooltip".Translate());
            
            if (autoPauseSettlements)
            {
                listingStandard.Gap(2f);
                listingStandard.Indent(24f);
                listingStandard.ColumnWidth -= 24f; // Fix indent overflow
                
                // Logic for Mutual Exclusion (Sync vs Strict Auto-Pause)
                // "Sync" implies Smart Auto-Pause (AutoPause=True, Sync=True)
                // "Strict" implies Dumb Auto-Pause (AutoPause=True, Sync=False)
                // "None" implies (AutoPause=False)

                bool uiSync = enablePocketMapSync;
                bool uiStrict = autoPausePocketMaps && !enablePocketMapSync;

                bool oldUiSync = uiSync;
                bool oldUiStrict = uiStrict;

                listingStandard.CheckboxLabeled("PauseTab_EnablePocketMapSync".Translate(), ref uiSync, "PauseTab_EnablePocketMapSyncTooltip".Translate());
                listingStandard.CheckboxLabeled("PauseTab_AutoPausePocketMaps".Translate(), ref uiStrict, "PauseTab_AutoPausePocketMapsTooltip".Translate());

                // Detect Changes
                if (uiSync != oldUiSync)
                {
                    if (uiSync)
                    {
                        // Sync turned ON -> Enable feature, Enable Sync
                        enablePocketMapSync = true;
                        autoPausePocketMaps = true;
                        uiStrict = false; // Visual update not strictly needed as we redraw next frame, but good for logic clarity
                    }
                    else
                    {
                        // Sync turned OFF -> Disable Sync. Should we disable AutoPause too? 
                        // User likely expects "Off" state.
                        enablePocketMapSync = false;
                        if (!uiStrict) autoPausePocketMaps = false; 
                    }
                }
                else if (uiStrict != oldUiStrict)
                {
                    if (uiStrict)
                    {
                        // Strict turned ON -> Enable feature, Disable Sync
                        autoPausePocketMaps = true;
                        enablePocketMapSync = false;
                    }
                    else
                    {
                        // Strict turned OFF -> Disable feature
                        autoPausePocketMaps = false;
                        // Sync is already false if we authorized strict change
                    }
                }

                listingStandard.ColumnWidth += 24f; // Restore width
                listingStandard.Outdent(24f);
            }
            
            listingStandard.GapLine();
            listingStandard.Label("FineGrainedControls".Translate());
            listingStandard.CheckboxLabeled("PauseOngoingJobs_Label".Translate(), ref PauseOngoingJobs, "PauseOngoingJobs_Tooltip".Translate());
            listingStandard.CheckboxLabeled("PauseHealth_Label".Translate(), ref PauseHealth, "PauseHealth_Tooltip".Translate());
            listingStandard.CheckboxLabeled("PauseWeather_Label".Translate(), ref PauseWeather, "PauseWeather_Tooltip".Translate());
            listingStandard.CheckboxLabeled("PauseMentalState_Label".Translate(), ref PauseMentalState, "PauseMentalState_Tooltip".Translate());
            listingStandard.CheckboxLabeled("PauseAgeing_Label".Translate(), ref PauseAgeing, "PauseAgeing_Tooltip".Translate());
            if (Compatibility_VehicleFramework.VehicleFrameworkActive)
            {
                listingStandard.GapLine();
                listingStandard.Label("Compatibility_Label".Translate());
                listingStandard.CheckboxLabeled("EnableVehicleFrameworkPatch_Label".Translate(), ref enableVehicleFrameworkPatch, "EnableVehicleFrameworkPatch_Tooltip".Translate());
            }
            listingStandard.End();
        }
    }
}