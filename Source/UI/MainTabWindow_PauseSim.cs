using Verse;
using UnityEngine;
using System.Linq;
using RimWorld.Planet;
using RimWorld;
using System.Collections.Generic;
using Verse.Sound;
using System.Reflection;

namespace PauseOtherSettlementsSimulation
{
    public class MainTabWindow_PauseSim : MainTabWindow
    {
        private Vector2 scrollPosition = Vector2.zero;
        public override Vector2 InitialSize => new Vector2(600f, 400f);

        public override void PreClose()
        {
            base.PreClose();
            LoadedModManager.GetMod<PauseOtherSettlementsSimulation>().WriteSettings();
        }

        public override void DoWindowContents(Rect inRect)
        {
            PauseOtherSettlementsSimulation.UpdateKnownSettlements();
            var settings = PauseOtherSettlementsSimulation.Settings;
			var playerSettlements = settings.knownSettlements;

            var anomalyMapsByParent = new Dictionary<int, List<Map>>();
            foreach (var map in Find.Maps)
            {
                // 기본: PocketMapParent → sourceMap의 정착지 묶기
                if (map.Parent is PocketMapParent pocketMapParent)
                {
                    Map sourceMap = pocketMapParent.sourceMap;
                    Settlement parentSettlement = sourceMap?.Parent as Settlement;
                    if (parentSettlement != null && parentSettlement.Faction == Faction.OfPlayer)
                    {
                        int parentTileId = parentSettlement.Tile;
                        if (!anomalyMapsByParent.ContainsKey(parentTileId))
                        {
                            anomalyMapsByParent[parentTileId] = new List<Map>();
                        }
                        anomalyMapsByParent[parentTileId].Add(map);
                        continue;
                    }
                }

                // 우주/특수 맵 등 PocketMapParent가 아닌데도 플레이어 정착지와 연계되지 않은 맵은
                // 독립 항목으로 표기하기 위해 가상의 키(-map.uniqueID)를 사용해 최하단에 표시
                if (!(map.Parent is Settlement) && map.IsPlayerHome)
                {
                    int pseudoKey = -map.uniqueID;
                    if (!anomalyMapsByParent.ContainsKey(pseudoKey))
                    {
                        anomalyMapsByParent[pseudoKey] = new List<Map>();
                    }
                    anomalyMapsByParent[pseudoKey].Add(map);
                }
            }

            Listing_Standard mainListing = new Listing_Standard();
            mainListing.Begin(inRect);

            // --- 제목과 체크박스를 한 줄에 표시하기 위한 수동 레이아웃 ---
            Rect headerRect = mainListing.GetRect(32f); // 제목을 위한 높이 확보
            Text.Font = GameFont.Medium;
            
            // 제목 라벨 그리기
            Vector2 titleSize = Text.CalcSize("PauseTab_Title".Translate());
            Rect titleRect = new Rect(headerRect.x, headerRect.y, titleSize.x, titleSize.y);
            Widgets.Label(titleRect, "PauseTab_Title".Translate());

            // 체크박스 그리기
            Text.Font = GameFont.Small; // 체크박스 라벨은 작은 폰트로
            Vector2 checkboxLabelSize = Text.CalcSize("PauseAnomalyLayersWhenAway_Label".Translate());
            float checkboxTotalWidth = checkboxLabelSize.x + 24f + 4f; // 라벨 + 체크박스 + 간격
            Rect checkboxRect = new Rect(headerRect.xMax - checkboxTotalWidth, headerRect.y, checkboxTotalWidth, titleRect.height);
            
            Widgets.CheckboxLabeled(checkboxRect, "PauseAnomalyLayersWhenAway_Label".Translate(), ref settings.pauseAnomalyLayersWhenAway);
            TooltipHandler.TipRegion(checkboxRect, "PauseAnomalyLayersWhenAway_Tooltip".Translate());

            mainListing.GapLine();
            // --- 수동 레이아웃 끝 ---


            if (!playerSettlements.Any())
            {
                mainListing.Label("PauseTab_NoSettlements".Translate());
            }
            else
            {
                DrawSettlementList(inRect, mainListing.CurHeight, playerSettlements, anomalyMapsByParent, settings);
            }
            mainListing.End();
        }

        private void DrawSettlementList(Rect inRect, float startY, List<SettlementInfo> settlements, Dictionary<int, List<Map>> anomalyMapsByParent, PauseOtherSettlementsSimulationSettings settings)
        {
            float totalContentHeight = 0;
            bool firstElementForHeight = true;
            foreach (var settlement in settlements)
            {
                if (!firstElementForHeight)
                {
                    totalContentHeight += 12f; // Gap height, same as GapLine(12f)
                }
                totalContentHeight += 24f; // RowHeight
                if (settings.settlementExpandedStates.TryGetValue(settlement.tile, out bool expanded) && expanded)
                {
                    if (anomalyMapsByParent.TryGetValue(settlement.tile, out var anomalyMaps))
                    {
                        totalContentHeight += anomalyMaps.Count * 24f;
                    }
                }
                firstElementForHeight = false;
            }

            Rect scrollViewHostRect = new Rect(inRect.x, startY, inRect.width, inRect.height - startY);
            Rect scrollViewContentRect = new Rect(0f, 0f, inRect.width - 16f, totalContentHeight);

            Widgets.BeginScrollView(scrollViewHostRect, ref scrollPosition, scrollViewContentRect);
            Listing_Standard settlementListing = new Listing_Standard();
            settlementListing.Begin(scrollViewContentRect);
            int currentMapTile = Find.CurrentMap?.Tile ?? -1;
            bool firstElement = true;
            foreach (var settlementInfo in settlements)
            {
                if (!firstElement)
                {
                    settlementListing.GapLine(12f);
                }
                var anomalyMaps = anomalyMapsByParent.ContainsKey(settlementInfo.tile) ? anomalyMapsByParent[settlementInfo.tile] : null;
                DrawSettlementRow(settlementListing, settlementInfo, anomalyMaps, settings, currentMapTile);
                firstElement = false;
            }
            // 부모 정착지에 묶이지 않은 특수 맵들을 추가로 표시
            foreach (var kv in anomalyMapsByParent.OrderBy(k => k.Key))
            {
                if (kv.Key >= 0) continue; // 정착지에 속한 키는 위에서 처리됨
                foreach (var map in kv.Value)
                {
                    Rect subRowRect = settlementListing.GetRect(24f);
                    string suffix = map.Tile.Layer.Def?.isSpace == true ? " (Orbit)" : " (Special)";
                    string label = Find.World.GetComponent<CustomNameWorldComponent>().GetCustomName(map) + suffix;
                    DrawSingleMapRow(subRowRect, label, -1, map, false, false, settings, currentMapTile);
                }
            }
            settlementListing.End();
            Widgets.EndScrollView();
        }

        private void DrawSettlementRow(Listing_Standard listing, SettlementInfo settlementInfo, List<Map> anomalyMaps, PauseOtherSettlementsSimulationSettings settings, int currentMapTile)
        {
            bool hasAnomalyMaps = anomalyMaps != null && anomalyMaps.Any();
            bool isExpanded = hasAnomalyMaps && settings.settlementExpandedStates.TryGetValue(settlementInfo.tile, out bool expanded) && expanded;
            DrawSingleMapRow(listing.GetRect(24f), settlementInfo.name, settlementInfo.tile, null, hasAnomalyMaps, isExpanded, settings, currentMapTile);
            if (isExpanded)
            {
                foreach (var map in anomalyMaps)
                {
                    Rect subRowRect = listing.GetRect(24f);
                    subRowRect.xMin += 20f;
                    // Get custom name from our WorldComponent, which is save-specific.
                    string anomalyLabel = Find.World.GetComponent<CustomNameWorldComponent>().GetCustomName(map);
                    DrawSingleMapRow(subRowRect, anomalyLabel, -1, map, false, false, settings, currentMapTile);
                }
            }
        }

        private void DrawSingleMapRow(Rect rowRect, string label, int tileId, Map map, bool hasChildren, bool isExpanded, PauseOtherSettlementsSimulationSettings settings, int currentMapTile)
        {
            Widgets.DrawHighlightIfMouseover(rowRect);
            GUI.BeginGroup(rowRect);

            const float iconWidth = 24f, checkboxWidth = 24f, expanderWidth = 24f, spacing = 4f;
            bool isParentSettlement = map == null;
            bool isCurrentMap = isParentSettlement ? (tileId == currentMapTile) : (map == Find.CurrentMap);
            float currentX = 0;

            // --- UI 요소들의 영역(Rect) 미리 계산 ---
            Rect expanderRect = new Rect(currentX, 0, hasChildren ? expanderWidth : 0, rowRect.height);
            currentX += expanderRect.width + (hasChildren ? spacing : 0);

            float rightSideWidth = iconWidth + checkboxWidth + (spacing * 2);
            float labelWidth = rowRect.width - currentX - rightSideWidth;
            Rect labelRect = new Rect(currentX, 0, labelWidth, rowRect.height);
            currentX += labelWidth + spacing;

            Rect renameRect = new Rect(currentX, 0, iconWidth, rowRect.height);
            currentX += iconWidth + spacing;
            
            Rect checkboxRect = new Rect(currentX, 0, checkboxWidth, rowRect.height);

            // --- 그리기 및 이벤트 처리 ---

            // 화살표 아이콘 (시각적)
            if (hasChildren)
            {
                Vector2 iconCenter = expanderRect.center;
                Matrix4x4 matrix = GUI.matrix;
                // 기본 상태(오른쪽 화살표)에서, 아코디언이 열려있을 때만 90도 회전시켜 아래를 보게 함
                if (isExpanded) UI.RotateAroundPivot(90f, iconCenter);
                GUI.DrawTexture(new Rect(iconCenter.x - TexButton.Reveal.width / 2f, iconCenter.y - TexButton.Reveal.height / 2f, TexButton.Reveal.width, TexButton.Reveal.height), TexButton.Reveal);
                GUI.matrix = matrix;
            }

            // 라벨 (아코디언이 있으면 버튼으로, 없으면 그냥 텍스트로)
            string labelText = label;
            if (isCurrentMap) labelText = $"{labelText} ({"CurrentMap".Translate()})";

            if (hasChildren)
            {
                // 배경 없는 텍스트 버튼으로 만들어 라벨 클릭 시 아코디언 토글
                if (Widgets.ButtonText(labelRect, labelText, drawBackground: false, doMouseoverSound: true, active: true))
                {
                    settings.settlementExpandedStates[tileId] = !isExpanded;
                }
            }
            else
            {
                Widgets.Label(labelRect, labelText);
            }

            // 이름 변경 버튼
            if (Widgets.ButtonImage(renameRect, TexButton.Rename))
            {
                if (isParentSettlement)
                {
                    var settlementToRename = Find.World.worldObjects.SettlementAt(tileId);
                    if (settlementToRename != null) Find.WindowStack.Add(new Dialog_RenameSettlementCustom(settlementToRename));
                }
                else if (map != null)
                {
                    Find.WindowStack.Add(new Dialog_RenameAnomalyLayerCustom(map));
                }
            }
            TooltipHandler.TipRegion(renameRect, "RenameSettlement".Translate());

            // 체크박스
            var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();
            bool isPaused;
            if (isParentSettlement)
            {
                isPaused = worldComp.settlementPausedStates.TryGetValue(tileId, out var ps) ? ps : settings.pauseNewSettlementsByDefault;
            }
            else // Anomaly map
            {
                if (worldComp.anomalyMapPausedStates.TryGetValue(map.uniqueID, out var aps))
                {
                    isPaused = aps;
                }
                else
                {
                    isPaused = settings.pauseNewAnomalyLayersByDefault;
                }
            }
            bool tempIsPaused = isPaused;
            Widgets.Checkbox(checkboxRect.position, ref tempIsPaused);
            if (tempIsPaused != isPaused)
            {
                if (isParentSettlement) PauseOtherSettlementsSimulation.SetSettlementPaused(tileId, tempIsPaused);
                else PauseOtherSettlementsSimulation.SetAnomalyMapPaused(map.uniqueID, tempIsPaused);
            }
            
            string tooltip = isCurrentMap ? "PauseTab_CurrentSettlementEditableTooltip".Translate() : (isParentSettlement ? "PauseTab_SettlementTooltip".Translate() : "PauseTab_AnomalyLayerTooltip".Translate());

            GUI.EndGroup(); // 그룹을 먼저 닫고

            // 그룹 밖에서, 원래 좌표계를 사용하는 툴팁을 등록합니다.
            TooltipHandler.TipRegion(rowRect, tooltip);
        }
    }
}
