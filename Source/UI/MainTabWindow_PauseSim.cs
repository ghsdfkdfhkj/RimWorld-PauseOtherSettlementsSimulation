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
        public override Vector2 InitialSize => new Vector2(700f, 400f); // Wider for time columns

        public override void PreClose()
        {
            base.PreClose();
            LoadedModManager.GetMod<PauseOtherSettlementsSimulation>().WriteSettings();
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Use direct query as requested to ensure all map parents (including space, camps) are captured.
            // Logic mirrors "currentPlayerMapParents" but keeps as list to avoid dictionary key collisions on Tile -1.
            var currentPlayerMapParents = Find.World.worldObjects.AllWorldObjects.OfType<MapParent>()
                .Where(mp => mp.Faction == Faction.OfPlayer && mp.HasMap)
                .ToList();

            var settings = PauseOtherSettlementsSimulation.Settings;
			
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
            Rect headerRect = mainListing.GetRect(32f); 
            Text.Font = GameFont.Medium;
            
            // 제목 라벨 그리기
            Vector2 titleSize = Text.CalcSize("PauseTab_Title".Translate());
            Rect titleRect = new Rect(headerRect.x, headerRect.y, titleSize.x, titleSize.y);
            Widgets.Label(titleRect, "PauseTab_Title".Translate());

            // 체크박스 그리기
            Text.Font = GameFont.Small; 
            Vector2 checkboxLabelSize = Text.CalcSize("PauseAnomalyLayersWhenAway_Label".Translate());
            float checkboxTotalWidth = checkboxLabelSize.x + 24f + 4f; 
            Rect checkboxRect = new Rect(headerRect.xMax - checkboxTotalWidth, headerRect.y, checkboxTotalWidth, titleRect.height);
            
            Widgets.CheckboxLabeled(checkboxRect, "PauseAnomalyLayersWhenAway_Label".Translate(), ref settings.pauseAnomalyLayersWhenAway);
            TooltipHandler.TipRegion(checkboxRect, "PauseAnomalyLayersWhenAway_Tooltip".Translate());

            mainListing.GapLine();
            // --- 수동 레이아웃 끝 ---


            if (!currentPlayerMapParents.Any())
            {
                mainListing.Label("PauseTab_NoSettlements".Translate());
            }
            else
            {
                DrawSettlementList(inRect, mainListing.CurHeight, currentPlayerMapParents, anomalyMapsByParent, settings);
            }
            mainListing.End();
        }

        private void DrawSettlementList(Rect inRect, float startY, List<MapParent> settlements, Dictionary<int, List<Map>> anomalyMapsByParent, PauseOtherSettlementsSimulationSettings settings)
        {
            float totalContentHeight = 0;
            bool firstElementForHeight = true;
            foreach (var settlement in settlements)
            {
                if (!firstElementForHeight)
                {
                    totalContentHeight += 12f; 
                }
                totalContentHeight += 24f; 
                if (settings.settlementExpandedStates.TryGetValue(settlement.Tile, out bool expanded) && expanded)
                {
                    if (anomalyMapsByParent.TryGetValue(settlement.Tile, out var anomalyMaps))
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
            foreach (var settlement in settlements)
            {
                if (!firstElement)
                {
                    settlementListing.GapLine(12f);
                }
                var anomalyMaps = anomalyMapsByParent.ContainsKey(settlement.Tile) ? anomalyMapsByParent[settlement.Tile] : null;
                DrawSettlementRow(settlementListing, settlement, anomalyMaps, settings, currentMapTile);
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
                    // Pass null as parent for independent maps in the catch-all loop
                    DrawSingleMapRow(subRowRect, label, -1, map, null, false, false, settings, currentMapTile);
                }
            }
            settlementListing.End();
            Widgets.EndScrollView();
        }

        private void DrawSettlementRow(Listing_Standard listing, MapParent settlement, List<Map> anomalyMaps, PauseOtherSettlementsSimulationSettings settings, int currentMapTile)
        {
            bool hasAnomalyMaps = anomalyMaps != null && anomalyMaps.Any();
            bool isExpanded = hasAnomalyMaps && settings.settlementExpandedStates.TryGetValue(settlement.Tile, out bool expanded) && expanded;
            // Pass the settlement object as 'parent' (4th arg)
            DrawSingleMapRow(listing.GetRect(24f), settlement.Label, settlement.Tile, null, settlement, hasAnomalyMaps, isExpanded, settings, currentMapTile);
            if (isExpanded)
            {
                foreach (var map in anomalyMaps)
                {
                    Rect subRowRect = listing.GetRect(24f);
                    subRowRect.xMin += 20f;
                    // Get custom name via WorldComponent
                    string anomalyLabel = Find.World.GetComponent<CustomNameWorldComponent>().GetCustomName(map);
                    // Pass null as parent for child maps
                    DrawSingleMapRow(subRowRect, anomalyLabel, -1, map, null, false, false, settings, currentMapTile);
                }
            }
        }

        private void DrawSingleMapRow(Rect rowRect, string label, int tileId, Map map, MapParent parent, bool hasChildren, bool isExpanded, PauseOtherSettlementsSimulationSettings settings, int currentMapTile)
        {
            Widgets.DrawHighlightIfMouseover(rowRect);
            GUI.BeginGroup(rowRect);

            const float iconWidth = 24f, checkboxWidth = 24f, expanderWidth = 24f, spacing = 8f;
            const float timeColumnWidth = 140f;
            const float offsetColumnWidth = 80f;

            bool isParentSettlement = parent != null; // It is a top-level settlement/map parent
            bool isCurrentMap = isParentSettlement ? (parent.Map == Find.CurrentMap) : (map == Find.CurrentMap);
            
            // If parent is present but Map is null (rare, but possible if map lost), handle gracefully
            if (isParentSettlement && parent.Map == null) isCurrentMap = false;

            float currentX = 0;

            // --- Layout Calculation ---
            Rect expanderRect = new Rect(currentX, 0, hasChildren ? expanderWidth : 0, rowRect.height);
            currentX += expanderRect.width + (hasChildren ? spacing : 0);

            float rightSideWidth = checkboxWidth + (spacing * 1);
            
            float centerAvailableWidth = rowRect.width - currentX - rightSideWidth;
            float availableForLabelRename = centerAvailableWidth - timeColumnWidth - offsetColumnWidth - (spacing * 2);
            float labelRenameWidth = availableForLabelRename - iconWidth - spacing;
            
            Rect labelRect = new Rect(currentX, 0, labelRenameWidth, rowRect.height);
            currentX += labelRenameWidth + spacing;

            Rect renameRect = new Rect(currentX, 0, iconWidth, rowRect.height);
            currentX += iconWidth + spacing;

            Rect timeRect = new Rect(currentX, 0, timeColumnWidth, rowRect.height);
            currentX += timeColumnWidth + spacing;

            Rect offsetRect = new Rect(currentX, 0, offsetColumnWidth, rowRect.height);
            currentX += offsetColumnWidth + spacing;

            Rect checkboxRect = new Rect(currentX, 0, checkboxWidth, rowRect.height);


            // --- Draw Elements ---

            // 1. Expander Arrow
            if (hasChildren)
            {
                Vector2 iconCenter = expanderRect.center;
                Matrix4x4 matrix = GUI.matrix;
                if (isExpanded) UI.RotateAroundPivot(90f, iconCenter);
                GUI.DrawTexture(new Rect(iconCenter.x - TexButton.Reveal.width / 2f, iconCenter.y - TexButton.Reveal.height / 2f, TexButton.Reveal.width, TexButton.Reveal.height), TexButton.Reveal);
                GUI.matrix = matrix;
            }

            // 2. Label
            string labelText = label;
            if (isCurrentMap) labelText = $"{labelText} ({"CurrentMap".Translate()})";

            if (hasChildren)
            {
                if (Widgets.ButtonText(labelRect, labelText, drawBackground: false, doMouseoverSound: true, active: true))
                {
                    settings.settlementExpandedStates[tileId] = !isExpanded;
                }
            }
            else
            {
                Widgets.Label(labelRect, labelText);
            }

            // 3. Rename Button
            if (Widgets.ButtonImage(renameRect, TexButton.Rename))
            {
                if (isParentSettlement && parent is Settlement s)
                {
                     Find.WindowStack.Add(new Dialog_RenameSettlementCustom(s));
                }
                else if (map != null)
                {
                    Find.WindowStack.Add(new Dialog_RenameAnomalyLayerCustom(map));
                }
                else if (isParentSettlement)
                {
                    // Generic MapParent rename support if needed, or disable. 
                    // Currently Dialog_RenameSettlementCustom takes 'Settlement'.
                    // If it's a generic MapParent (like Ship), we might not support renaming or need a generic dialog.
                    // For now, suppress if not Settlement to avoid crash. (Space ships usually rename via their own UI).
                }
            }
            TooltipHandler.TipRegion(renameRect, "RenameSettlement".Translate());

            // 4. Time & Offset Columns
            // Use passed map, or derive from parent
            Map targetMap = map ?? parent?.Map;
            
            if (targetMap != null)
            {
                // Local Time
                long localTicks = LocalTimeManager.GetLocalTicksAbs(targetMap);
                
                Vector2 longLat = Vector2.zero;
                if (targetMap.Tile >= 0)
                {
                    longLat = Find.WorldGrid.LongLatOf(targetMap.Tile);
                }
                
                string dateStr = GenDate.DateFullStringAt(localTicks, longLat);
                int hour = GenDate.HourOfDay(localTicks, longLat.x);
                string timeStr = $"{dateStr}, {hour}h";
                
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.Font = GameFont.Tiny;
                Widgets.Label(timeRect, timeStr);

                // Time Offset
                int globalTicks = Find.TickManager.TicksAbs;
                long diffTicks = globalTicks - localTicks;
                string offsetStr = "-";
                
                if (diffTicks > 0)
                {
                    float diffDays = diffTicks / 60000f;
                    offsetStr = $"-{diffDays:F1}d";
                    GUI.color = Color.gray;
                }
                else
                {
                    GUI.color = Color.white;
                }
                
                Widgets.Label(offsetRect, offsetStr);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }


            // 5. Checkbox
            var worldComp = Find.World.GetComponent<CustomNameWorldComponent>();
            bool isPaused;
            if (isParentSettlement)
            {
                // Use tileId (which is settlement.Tile) for top-level pause state
                isPaused = worldComp.settlementPausedStates.TryGetValue(tileId, out var ps) ? ps : settings.pauseNewSettlementsByDefault;
            }
            else
            {
                // Use map.uniqueID for child maps
                if (worldComp.anomalyMapPausedStates.TryGetValue(map.uniqueID, out var aps)) isPaused = aps;
                else isPaused = settings.pauseNewAnomalyLayersByDefault;
            }
            bool tempIsPaused = isPaused;
            Widgets.Checkbox(checkboxRect.position, ref tempIsPaused);
            if (tempIsPaused != isPaused)
            {
                if (isParentSettlement) PauseOtherSettlementsSimulation.SetSettlementPaused(tileId, tempIsPaused);
                // Note: If SOS2 ship shares Tile -1, this will pause ALL ships sharing Tile -1.
                // This is a known potential limitation but consistent with current "SetSettlementPaused" logic.
                else PauseOtherSettlementsSimulation.SetAnomalyMapPaused(map.uniqueID, tempIsPaused);
            }
            
            string tooltip = isCurrentMap ? "PauseTab_CurrentSettlementEditableTooltip".Translate() : (isParentSettlement ? "PauseTab_SettlementTooltip".Translate() : "PauseTab_AnomalyLayerTooltip".Translate());

            GUI.EndGroup();
            TooltipHandler.TipRegion(rowRect, tooltip);
        }
    }
}
