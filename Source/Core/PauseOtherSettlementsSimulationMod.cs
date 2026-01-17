using HarmonyLib;
using UnityEngine;
using Verse;

namespace PauseOtherSettlementsSimulation
{
    [StaticConstructorOnStartup]
    public class PauseOtherSettlementsSimulation : Mod
    {
        public static PauseOtherSettlementsSimulationSettings Settings { get; private set; }

        public PauseOtherSettlementsSimulation(ModContentPack content) : base(content)
        {
            Settings = GetSettings<PauseOtherSettlementsSimulationSettings>();
        }

        public override string SettingsCategory() => "PauseOtherSettlementsSimulation".Translate();

        public override void DoSettingsWindowContents(Rect inRect) {
            Settings.DoWindowContents(inRect);
            SimulationManager.UpdateKnownSettlements();
        }
    }
}
