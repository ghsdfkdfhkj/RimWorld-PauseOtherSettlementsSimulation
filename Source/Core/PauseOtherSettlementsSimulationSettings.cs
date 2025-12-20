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
        public bool pauseNewAnomalyLayersByDefault = false; // New setting, default off
        public bool PauseOtherSettlements = true;
        public bool PauseOngoingJobs = true;
        public bool PauseHealth = true;
        public bool PauseWeather = true;
        public bool PauseMentalState = true;
        public bool PauseAgeing = true;
        public bool pauseNewSettlementsByDefault = true; // New Setting
        public bool pauseAnomalyLayersWhenAway = true; // New setting
        public bool enableVehicleFrameworkPatch = true; // VF Patch Toggle
        public Dictionary<int, bool> settlementExpandedStates = new Dictionary<int, bool>(); // For accordion UI state
        public List<SettlementInfo> knownSettlements = new List<SettlementInfo>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pauseNewAnomalyLayersByDefault, "pauseNewAnomalyLayersByDefault", false);
            Scribe_Values.Look(ref PauseOtherSettlements, "pauseOtherSettlements", true);
            Scribe_Values.Look(ref PauseOngoingJobs, "pauseOngoingJobs", true);
            Scribe_Values.Look(ref PauseHealth, "pauseHealth", true);
            Scribe_Values.Look(ref PauseWeather, "pauseWeather", true);
            Scribe_Values.Look(ref PauseMentalState, "pauseMentalState", true);
            Scribe_Values.Look(ref PauseAgeing, "pauseAgeing", true);
            Scribe_Values.Look(ref pauseNewSettlementsByDefault, "pauseNewSettlementsByDefault", true); // New Setting
            Scribe_Values.Look(ref pauseAnomalyLayersWhenAway, "pauseAnomalyLayersWhenAway", true); // New setting
            Scribe_Values.Look(ref enableVehicleFrameworkPatch, "enableVehicleFrameworkPatch", true);
            Scribe_Collections.Look(ref settlementExpandedStates, "settlementExpandedStates", LookMode.Value, LookMode.Value);
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
            listingStandard.CheckboxLabeled("PauseOtherSettlementsSimulation".Translate(), ref PauseOtherSettlements, "PauseOtherSettlements_Tooltip".Translate());
            listingStandard.CheckboxLabeled("PauseNewSettlementsByDefault_Label".Translate(), ref pauseNewSettlementsByDefault, "PauseNewSettlementsByDefault_Tooltip".Translate());
            listingStandard.CheckboxLabeled("PauseNewAnomalyLayersByDefault_Label".Translate(), ref pauseNewAnomalyLayersByDefault, "PauseNewAnomalyLayersByDefault_Tooltip".Translate());
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