using HarmonyLib;
using System;
using System.Reflection.Emit;
using Verse;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using Verse.Sound;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse.AI;

namespace PauseOtherSettlementsSimulation
{
    [StaticConstructorOnStartup]
    public class PauseOtherSettlementsSimulation : Mod
    {
        public static PauseOtherSettlementsSimulationSettings Settings { get; private set; }
        private static int simulationCacheTick = -1;
			public static readonly Dictionary<int, bool> mapSimulationCache = new Dictionary<int, bool>();
			private static readonly HashSet<int> mapsCurrentlyPaused = new HashSet<int>();

        public PauseOtherSettlementsSimulation(ModContentPack content) : base(content)
        {
            Settings = GetSettings<PauseOtherSettlementsSimulationSettings>();
        }



        public override string SettingsCategory() => "PauseOtherSettlementsSimulation".Translate();

        public override void DoSettingsWindowContents(Rect inRect) {
            Settings.DoWindowContents(inRect);
            UpdateKnownSettlements();
        }

        public static void UpdateKnownSettlements()
        {
            if (Find.World?.worldObjects?.Settlements == null)
            {
                Settings.knownSettlements.Clear();
                return;
            }

            var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();

            // Get all current player settlements from the world (surface)
            // Get all current player owned map parents (Settlements, Camps, Space Stations, etc.)
            // We only care about things that have a map (or are generating one) and belong to the player.
            // Some mods might use MapParent for camps without them being "Settlement" class.
            var currentPlayerMapParents = Find.World.worldObjects.AllWorldObjects.OfType<MapParent>()
                .Where(mp => mp.Faction == Faction.OfPlayer && mp.HasMap)
                .ToDictionary(mp => mp.Tile, mp => mp.Label);

            // Remove settlements from our list that no longer exist in the world
            Settings.knownSettlements.RemoveAll(s => !currentPlayerMapParents.ContainsKey(s.tile));

            // Update names for existing entries and add new ones (surface + off-world)
            foreach (var kv in currentPlayerMapParents)
            {
                int tile = kv.Key;
                string name = kv.Value;
                var existing = Settings.knownSettlements.FirstOrDefault(s => s.tile == tile);
                if (existing != null)
                {
                    if (existing.name != name)
                    {
                        existing.name = name;
                    }
                }
                else
                {
                    var newInfo = new SettlementInfo { tile = tile, name = name };
                    Settings.knownSettlements.Add(newInfo);
                    if (!worldComp.settlementPausedStates.ContainsKey(tile))
                    {
                        // Default to running (false). Auto-pause will catch it if needed.
                        worldComp.settlementPausedStates[tile] = false;
                    }
                }
            }
        }

		public static bool ShouldSimulateMap(Map map)
        {
            if (map == null || map == Find.CurrentMap) return true;
            if (!Settings.PauseOtherSettlements) return true;

            var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();
            bool isPaused;
            if (map.Parent is PocketMapParent pocket)
            {
                // [Sync Feature] If sync is enabled, the pocket map simply mirrors its source map's state.
                // This handles cases where the source map is the CurrentMap (Simulate=true) 
                // or is another simulating map.
                if (Settings.enablePocketMapSync && pocket.sourceMap != null)
                {
                    return ShouldSimulateMap(pocket.sourceMap);
                }

                if (worldComp.anomalyMapPausedStates.TryGetValue(map.uniqueID, out bool pausedState))
                {
                    isPaused = pausedState;
                }
                else
                {
                    isPaused = false;
                }
            }
            else if (map.Parent is Settlement settlement)
            {
                isPaused = worldComp.settlementPausedStates.TryGetValue(settlement.Tile, out bool pausedState) ? pausedState : false;
            }
            // Add support for generic MapParents that are player-owned (Camps, etc.)
            else if (map.Parent is MapParent mapParent && mapParent.Faction == Faction.OfPlayer)
            {
                 isPaused = worldComp.settlementPausedStates.TryGetValue(mapParent.Tile, out bool pausedState) ? pausedState : false;
            }
			else
			{
				// 기타 맵(우주/특수 맵 등)은 map.uniqueID 기준으로 제어
				isPaused = worldComp.anomalyMapPausedStates.TryGetValue(map.uniqueID, out bool pausedState)
					? pausedState
					: false;
			}
			return !isPaused;
        }

			public static void ApplyMapPauseState(Map map, bool paused)
			{
				if (map == null) return;
				bool already = mapsCurrentlyPaused.Contains(map.uniqueID);
                var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();

				if (paused)
				{
                    // Time tracking: Record when pause started if not already recorded
                    if (worldComp != null && !worldComp.mapLastPauseTick.ContainsKey(map.uniqueID))
                    {
                        worldComp.mapLastPauseTick[map.uniqueID] = Find.TickManager.TicksGame;
                    }

					if (!already)
					{
						// Remove all tickables on this map from tick lists
						Find.TickManager.RemoveAllFromMap(map);
						mapsCurrentlyPaused.Add(map.uniqueID);
					}
				}
				else
				{
                    // Time tracking: Calculate duration and add to total
                    if (worldComp != null && worldComp.mapLastPauseTick.TryGetValue(map.uniqueID, out int lastTick))
                    {
                        int duration = Find.TickManager.TicksGame - lastTick;
                        if (duration > 0)
                        {
                            if (!worldComp.mapTotalPausedTicks.ContainsKey(map.uniqueID)) worldComp.mapTotalPausedTicks[map.uniqueID] = 0;
                            worldComp.mapTotalPausedTicks[map.uniqueID] += duration;
                        }
                        worldComp.mapLastPauseTick.Remove(map.uniqueID);
                    }

					if (already)
					{
						// Re-register all things on this map
						var allThings = map.listerThings.AllThings;
						for (int i = 0; i < allThings.Count; i++)
						{
							Find.TickManager.RegisterAllTickabilityFor(allThings[i]);
						}
						mapsCurrentlyPaused.Remove(map.uniqueID);
					}
				}
			}

			public static void SetSettlementPaused(int tileId, bool paused)
			{
				var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();
				bool old = worldComp.settlementPausedStates.TryGetValue(tileId, out var prev) ? prev : false;
				if (old == paused) return;
				worldComp.settlementPausedStates[tileId] = paused;
				var settlement = Find.World.worldObjects.SettlementAt(tileId);
				if (settlement != null && settlement.HasMap)
				{
					ApplyMapPauseState(settlement.Map, paused);
				}
			}

			public static void SetAnomalyMapPaused(int mapUniqueId, bool paused)
			{
				var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();
				bool old = worldComp.anomalyMapPausedStates.TryGetValue(mapUniqueId, out var prev) ? prev : false;
				if (old == paused) return;
				worldComp.anomalyMapPausedStates[mapUniqueId] = paused;
				var map = Find.Maps.FirstOrDefault(m => m.uniqueID == mapUniqueId);
				if (map != null)
				{
					ApplyMapPauseState(map, paused);
				}
			}

        public static void PrecomputeMapSimulationCache()
        {
            int tick = Find.TickManager.TicksGame;
            if (simulationCacheTick == tick) return;
            simulationCacheTick = tick;
            mapSimulationCache.Clear();
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                var m = maps[i];
                bool sim = true;
                try { sim = ShouldSimulateMap(m); }
                catch { sim = true; }
                mapSimulationCache[m.uniqueID] = sim;
            }
        }

        public static bool IsMapSimulatingCached(Map map)
        {
            if (map == null) return true;
            int tick = Find.TickManager.TicksGame;
            if (simulationCacheTick != tick)
            {
                PrecomputeMapSimulationCache();
            }
            if (mapSimulationCache.TryGetValue(map.uniqueID, out bool sim)) return sim;
            return true; // 캐시에 없다면 안전하게 허용
        }
    }



    // TickList.Tick의 본문을 유지하면서 thing.DoTick() 직전에 가드 분기를 삽입합니다.
    [HarmonyPatch(typeof(TickList), "Tick")]
    public static class TickList_Tick_Transpiler
    {
        private static readonly MethodInfo miDoTick = AccessTools.Method(typeof(Thing), nameof(Thing.DoTick));
        private static readonly MethodInfo miShould = AccessTools.Method(typeof(PauseOtherSettlementsSimulation), nameof(PauseOtherSettlementsSimulation.IsMapSimulatingCached));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(miDoTick))
                {
                    // callvirt Thing.DoTick() 앞에
                    Label afterCall = il.DefineLabel();
                    // 다음 명령어에 라벨 부착
                    int nextIndex = i + 1;
                    if (nextIndex < codes.Count)
                    {
                        codes[nextIndex].labels.Add(afterCall);
                    }
                    // 삽입:
                    // dup
                    // callvirt instance Map Verse.Thing::get_Map()
                    // call bool PauseOtherSettlementsSimulation.IsMapSimulatingCached(Map)
                    // brfalse.s afterCall
                    var injected = new List<CodeInstruction>
                    {
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map))),
                        new CodeInstruction(OpCodes.Call, miShould),
                        new CodeInstruction(OpCodes.Brfalse_S, afterCall)
                    };
                    codes.InsertRange(i, injected);
                    i += injected.Count; // 건너뛰기
                }
            }
            return codes;
        }
    }

    [HarmonyPatch(typeof(TickManager), "DoSingleTick")]
    public static class TickManager_DoSingleTick_CachePatch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            PauseOtherSettlementsSimulation.PrecomputeMapSimulationCache();
        }
    }

    [HarmonyPatch(typeof(Map), "MapPostTick")]
    public static class MapPostTickPatch { [HarmonyPrefix] public static bool Prefix(Map __instance) => !PauseOtherSettlementsSimulation.Settings.PauseWeather || PauseOtherSettlementsSimulation.ShouldSimulateMap(__instance); }

    [HarmonyPatch(typeof(Storyteller), "StorytellerTick")]
    public static class StorytellerTickPatch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            // Storyteller a always ticking is safer to prevent unexpected time jump bugs.
            // The IncidentQueueTickPatch will handle preventing incidents on paused maps.
            return true;
        }
    }

    [HarmonyPatch(typeof(IncidentQueue), "IncidentQueueTick")]
    public static class IncidentQueueTickPatch 
    { 
        [HarmonyPrefix] 
        public static bool Prefix(IncidentQueue __instance)
        {
            if (!PauseOtherSettlementsSimulation.Settings.PauseWeather) return true;

            foreach (QueuedIncident queuedIncident in __instance)
            {
                if (queuedIncident?.FiringIncident?.parms?.target is Map map && PauseOtherSettlementsSimulation.ShouldSimulateMap(map))
                {
                    return true; // 시뮬레이션될 맵을 대상으로 하는 대기중인 이벤트가 있으면 틱을 실행합니다.
                }
            }

            // 시뮬레이션될 맵을 대상으로 하는 대기중인 이벤트가 없으면 틱을 실행하지 않습니다.
            // 단, 월드(캐러밴) 대상 이벤트 등은 계속 처리되어야 하므로, 큐가 비어있지 않다면 틱을 허용하는 것이 안전할 수 있습니다.
            // 우선은 맵 대상 이벤트만 확인하여 제어합니다.
            // 만약 맵을 대상으로 하지 않는 이벤트가 있다면, 큐에 있어도 여기서 실행되지 않을 수 있습니다.
            // 하지만 대부분의 큐에 있는 이벤트는 맵을 대상으로 하므로 이 방법이 효과적일 것입니다.
            // 더 안전한 방법은 모든 큐를 확인하고 맵이 아닌 타겟은 항상 허용하는 것입니다.
            bool shouldTick = true;
            foreach (QueuedIncident item in __instance)
            {
                if (item.FiringIncident.parms.target is Map map)
                {
                    if (PauseOtherSettlementsSimulation.ShouldSimulateMap(map))
                    {
                        return true;
                    }
                    shouldTick = false; // 시뮬레이션이 꺼진 맵 대상 이벤트가 있으니 일단 보류
                }
                else
                {
                    return true; // 맵이 아닌 다른 대상(예: 월드) 이벤트는 항상 실행
                }
            }

            return shouldTick;
        }
    }

    [HarmonyPatch(typeof(Pawn_AgeTracker), "AgeTickInterval")]
    public static class Pawn_AgeTracker_AgeTickInterval_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn)
        {
            if (!PauseOtherSettlementsSimulation.Settings.PauseAgeing) return true;
            return PauseOtherSettlementsSimulation.ShouldSimulateMap(___pawn?.Map);
        }
    }

    [HarmonyPatch(typeof(Gene_Hemogen), "TickInterval")]
    public static class Gene_Hemogen_TickInterval_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Gene_Hemogen __instance)
        {
            return PauseOtherSettlementsSimulation.ShouldSimulateMap(__instance.pawn?.Map);
        }
    }

    [HarmonyPatch(typeof(JobDriver), "DriverTick")]
    public static class JobDriverTickPatch { [HarmonyPrefix] public static bool Prefix(JobDriver __instance) => !PauseOtherSettlementsSimulation.Settings.PauseOngoingJobs || __instance.pawn?.Map == null || PauseOtherSettlementsSimulation.ShouldSimulateMap(__instance.pawn.Map); }

    [HarmonyPatch(typeof(JobDriver), "DriverTickInterval")]
    public static class JobDriverTickIntervalPatch { [HarmonyPrefix] public static bool Prefix(JobDriver __instance) => !PauseOtherSettlementsSimulation.Settings.PauseOngoingJobs || __instance.pawn?.Map == null || PauseOtherSettlementsSimulation.ShouldSimulateMap(__instance.pawn.Map); }

    [HarmonyPatch(typeof(Pawn_TrainingTracker), "TrainingTrackerTickRare")]
    public static class Pawn_TrainingTracker_TrainingTrackerTickRare_Patch
    {
        private static FieldInfo countDecayFromField = AccessTools.Field(typeof(Pawn_TrainingTracker), "countDecayFrom");

        [HarmonyPrefix]
        public static bool Prefix(Pawn_TrainingTracker __instance, Pawn ___pawn)
        {
            if (PauseOtherSettlementsSimulation.ShouldSimulateMap(___pawn?.Map)) return true;

            // 맵이 정지된 동안에는 훈련 감퇴 타이머만 흐르게 하여(250틱 추가),
            // 나중에 맵이 다시 로드되었을 때 급격한 감퇴가 일어나지 않도록 보호합니다.
            // 이는 Suspended 상태일 때의 로직과 동일합니다.
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
            return PauseOtherSettlementsSimulation.ShouldSimulateMap(___pawn?.Map);
        }
    }

    [HarmonyPatch(typeof(Pawn_NeedsTracker), "NeedsTrackerTickInterval")]
    public static class NeedsTrackerTickIntervalPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn)
        {
            return PauseOtherSettlementsSimulation.ShouldSimulateMap(___pawn?.Map);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "HealthTick")]
    public static class HealthTickPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn)
        {
            if (!PauseOtherSettlementsSimulation.Settings.PauseHealth) return true;
            return PauseOtherSettlementsSimulation.ShouldSimulateMap(___pawn?.Map);
        }
    }
    
    [HarmonyPatch(typeof(Pawn_HealthTracker), "HealthTickInterval")]
    public static class HealthTickIntervalPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn)
        {
            if (!PauseOtherSettlementsSimulation.Settings.PauseHealth) return true;
            return PauseOtherSettlementsSimulation.ShouldSimulateMap(___pawn?.Map);
        }
    }

    [HarmonyPatch(typeof(Pawn_MindState), "MindStateTickInterval")]
    public static class Pawn_MindState_MindStateTickInterval_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn_MindState __instance)
        {
            if (!PauseOtherSettlementsSimulation.Settings.PauseMentalState) return true;
            return PauseOtherSettlementsSimulation.ShouldSimulateMap(__instance.pawn?.Map);
        }
    }

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
                    !PauseOtherSettlementsSimulation.IsMapSimulatingCached(__instance.info.Maker.Thing.Map))
                {
                    __instance.End();
                    return false; // Abort original method.
                }
            }

            // For all other sustainers, or for FleshmassAmbience on a simulating map, run the original method.
            return true;
        }
    }

    [HarmonyPatch(typeof(RoomTempTracker), "EqualizeTemperature")]
    public static class RoomTempTracker_EqualizeTemperature_Patch
    {
        private static FieldInfo roomField = AccessTools.Field(typeof(RoomTempTracker), "room");

        [HarmonyPrefix]
        public static bool Prefix(RoomTempTracker __instance)
        {
            var room = (Room)roomField.GetValue(__instance);
            if (room == null || room.Map == null) return true;

            return PauseOtherSettlementsSimulation.ShouldSimulateMap(room.Map);
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
                if (!PauseOtherSettlementsSimulation.ShouldSimulateMap(map))
                {
                    return false; // This aborts the original TryExecute method, preventing the incident.
                }
            }

            // Allow the incident to execute for all other cases (e.g., world targets, caravans, or active maps).
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
            // 맵이 정지되었다가 다시 시작될 때, 기존의 soundPlaying Sustainer는 이미 죽어있을 수 있습니다.
            // 죽은 Sustainer를 Maintain하려고 하면 에러가 발생하므로, 미리 확인하여 null로 초기화해줍니다.
            // 이렇게 하면 원본 Tick에서 새로운 Sustainer를 생성하게 됩니다.
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
