using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace PauseOtherSettlementsSimulation
{
    // This is a custom implementation of the rename dialog, specifically for Settlements,
    // because the original Dialog_Rename is an abstract generic class and its derivatives are not public.
    public class Dialog_RenameSettlementCustom : Window
    {
        protected string curName;
        private bool focusedRenameField;
        private int startAcceptingInputAtFrame;
        protected readonly Settlement settlement;

        private bool AcceptsInput => startAcceptingInputAtFrame <= Time.frameCount;
        protected virtual int MaxNameLength => 28;
        public override Vector2 InitialSize => new Vector2(280f, 175f);

        public Dialog_RenameSettlementCustom(Settlement settlement)
        {
            this.settlement = settlement;
            this.curName = settlement.Name; // Use the Name property from INameableWorldObject
            this.doCloseX = true;
            this.forcePause = true;
            this.closeOnAccept = false;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
        }

        public void WasOpenedByHotkey()
        {
            startAcceptingInputAtFrame = Time.frameCount + 1;
        }

        protected virtual AcceptanceReport NameIsValid(string name)
        {
            if (name.Length == 0)
            {
                return "NameCannotBeEmpty".Translate();
            }
            return true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            bool flag = false;
            if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                flag = true;
                Event.current.Use();
            }

            // Title: "Rename"
            Rect titleRect = new Rect(inRect);
            Text.Font = GameFont.Medium;
            titleRect.height = Text.LineHeight + 10f;
            Widgets.Label(titleRect, "Rename".Translate());
            Text.Font = GameFont.Small;

            // Text Field
            GUI.SetNextControlName("RenameField");
            string text = Widgets.TextField(new Rect(0f, titleRect.height, inRect.width, 35f), curName);
            if (AcceptsInput && text.Length < MaxNameLength)
            {
                curName = text;
            }
            else if (!AcceptsInput)
            {
                ((TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl)).SelectAll();
            }

            if (!focusedRenameField)
            {
                UI.FocusControl("RenameField", this);
                focusedRenameField = true;
            }

            // OK Button
            if (Widgets.ButtonText(new Rect(15f, inRect.height - 35f - 10f, inRect.width - 30f, 35f), "OK".Translate()) || flag)
            {
                AcceptanceReport acceptanceReport = NameIsValid(curName);
                if (!acceptanceReport.Accepted)
                {
                    if (acceptanceReport.Reason.NullOrEmpty())
                    {
                        Messages.Message("NameIsInvalid".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    }
                    else
                    {
                        Messages.Message(acceptanceReport.Reason, MessageTypeDefOf.RejectInput, historical: false);
                    }
                    return;
                }
                
                // Set the new name
                if (this.settlement != null)
                {
                    this.settlement.Name = curName; // Use the Name property
                }

                Find.WindowStack.TryRemove(this);
            }
        }
    }
}
