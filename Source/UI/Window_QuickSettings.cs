using UnityEngine;
using Verse;
using RimWorld;

namespace PauseOtherSettlementsSimulation
{
    public class Window_QuickSettings : Window
    {
        public override Vector2 InitialSize => new Vector2(350f, 250f);
        private Rect anchorRect;

        public Window_QuickSettings(Rect anchorRect)
        {
            this.anchorRect = anchorRect;
            this.doCloseX = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = false;
        }
        
        protected override void SetInitialSizeAndPosition()
        {
             // Start with standard behavior to get size
             base.SetInitialSizeAndPosition();

             // Now override position
             // Anchor is the button. We want to place it RELATIVE to the MainTab window, not just the button.
             // But the button is IN the MainTab window.
             // If we can get the MainTabWindow's rect, that's better.
             
             // Strategy: Try to place to the RIGHT of the MainTabWindow.
             // MainTabWindow usually sits at the bottom.
             // Find the MainTabWindow instance.
             MainTabWindow_PauseSim mainTab = Find.WindowStack.WindowOfType<MainTabWindow_PauseSim>();
             if (mainTab != null)
             {
                 Rect parentRect = mainTab.windowRect;
                 
                 // Try placing to the Right
                 float x = parentRect.xMax + 5f;
                 float y = parentRect.y; // Align top?
                 
                 // If off screen to right, try Left?
                 if (x + InitialSize.x > UI.screenWidth)
                 {
                     x = parentRect.x - InitialSize.x - 5f;
                 }
                 
                 // If parent is at bottom, maybe align bottom?
                 // Let's align bottom of this window = bottom of parent window if parent is at bottom?
                 // Usually main tabs are full height or fixed height at bottom.
                 // Align Y to be somewhat centered or top-aligned with parent.
                 // Let's just align Top for now.
                 
                 // Check if off-screen vertically
                 if (y + InitialSize.y > UI.screenHeight)
                 {
                     y = UI.screenHeight - InitialSize.y;
                 }
                 
                 this.windowRect = new Rect(x, y, InitialSize.x, InitialSize.y);
             }
             else
             {
                 // Fallback to mouse position or center
                 // But constructor took anchorRect (button). Use that if mainTab is somehow null (unlikely calling from mainTab)
                 this.windowRect = new Rect(anchorRect.xMax + 5f, anchorRect.y, InitialSize.x, InitialSize.y);
             }
             
             // Ensure inside screen (clamp)
             this.windowRect = this.windowRect.Rounded();
             this.windowRect.x = Mathf.Clamp(this.windowRect.x, 0, UI.screenWidth - this.windowRect.width);
             this.windowRect.y = Mathf.Clamp(this.windowRect.y, 0, UI.screenHeight - this.windowRect.height);
        }

        public override void DoWindowContents(Rect inRect)
        {
            var settings = PauseOtherSettlementsSimulation.Settings;
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("PauseTab_Title".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine();

            // Auto-Pause Settlements
            listing.CheckboxLabeled("PauseTab_AutoPauseSettlements".Translate(), ref settings.autoPauseSettlements, "PauseTab_AutoPauseSettlementsTooltip".Translate());

            if (settings.autoPauseSettlements)
            {
                listing.Gap(4f);
                listing.Indent(24f);
                listing.ColumnWidth -= 24f;

                // Logic for Mutual Exclusion (Sync vs Strict Auto-Pause) - Mirrored from Settings
                bool uiSync = settings.enablePocketMapSync;
                bool uiStrict = settings.autoPausePocketMaps && !settings.enablePocketMapSync;

                bool oldUiSync = uiSync;
                bool oldUiStrict = uiStrict;

                listing.CheckboxLabeled("PauseTab_EnablePocketMapSync".Translate(), ref uiSync, "PauseTab_EnablePocketMapSyncTooltip".Translate());
                listing.CheckboxLabeled("PauseTab_AutoPausePocketMaps".Translate(), ref uiStrict, "PauseTab_AutoPausePocketMapsTooltip".Translate());

                if (uiSync != oldUiSync)
                {
                    if (uiSync)
                    {
                        settings.enablePocketMapSync = true;
                        settings.autoPausePocketMaps = true;
                        uiStrict = false; 
                    }
                    else
                    {
                        settings.enablePocketMapSync = false;
                        if (!uiStrict) settings.autoPausePocketMaps = false; 
                    }
                }
                else if (uiStrict != oldUiStrict)
                {
                    if (uiStrict)
                    {
                        settings.autoPausePocketMaps = true;
                        settings.enablePocketMapSync = false;
                    }
                    else
                    {
                        settings.autoPausePocketMaps = false;
                    }
                }

                listing.ColumnWidth += 24f;
                listing.Outdent(24f);
            }

            listing.GapLine();
            
            if (listing.ButtonText("Close".Translate()))
            {
                Close();
            }

            listing.End();
            
            // Apply settings immediately if needed (Saving is usually done on mod close or explicitly)
            // But we should probably write settings when closing this window or changing values?
            // LoadedModManager.GetMod<PauseOtherSettlementsSimulation>().WriteSettings();
            // Writing on every frame is bad. But WriteSettings() is cheap usually.
            // Let's rely on the main window closing -> WriteSettings (MainTabWindow_PauseSim.PreClose calls it).
            // But if user changes here and doesn't close MainTab, it might not save if game crashes?
            // Standard mod settings practice: Write on Close? 
        }

        public override void PreClose()
        {
            base.PreClose();
            LoadedModManager.GetMod<PauseOtherSettlementsSimulation>().WriteSettings();
        }
    }
}
