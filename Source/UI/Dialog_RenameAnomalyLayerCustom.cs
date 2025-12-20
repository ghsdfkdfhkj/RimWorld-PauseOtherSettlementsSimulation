using UnityEngine;
using Verse;
using RimWorld;

namespace PauseOtherSettlementsSimulation
{
    // This dialog now interacts with our WorldComponent to handle custom names.
    public class Dialog_RenameAnomalyLayerCustom : Window
    {
        protected string curName;
        private readonly Map map;

        public override Vector2 InitialSize => new Vector2(280f, 175f);

        public Dialog_RenameAnomalyLayerCustom(Map map)
        {
            this.map = map;
            this.curName = Find.World.GetComponent<CustomNameWorldComponent>().GetCustomName(map);
            
            this.doCloseX = true;
            this.forcePause = true; // This will pause the game automatically.
            this.closeOnAccept = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
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

            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "Rename".Translate());
            curName = Widgets.TextField(new Rect(0f, 35f, inRect.width, 35f), curName);

            if (Widgets.ButtonText(new Rect(0, inRect.height - 35f, inRect.width / 2f - 5f, 35f), "OK".Translate()) || flag)
            {
                if (string.IsNullOrEmpty(curName))
                {
                    Messages.Message("NameCannotBeEmpty".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
                
                // Save the custom name to our WorldComponent, which is part of the save file.
                Find.World.GetComponent<CustomNameWorldComponent>().SetCustomName(map, curName);
                
                this.Close();
            }

            if (Widgets.ButtonText(new Rect(inRect.width / 2f + 5f, inRect.height - 35f, inRect.width / 2f - 5f, 35f), "Cancel".Translate()))
            {
                this.Close();
            }
        }
    }
}
