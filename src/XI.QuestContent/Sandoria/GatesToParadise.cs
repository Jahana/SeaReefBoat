// Gates to Paradise (Log ID 0, Quest ID 18), ported from the quest branches of
// scripts/zones/Northern_San_dOria/npcs/Olbergieut.lua and scripts/zones/La_Theine_Plateau/npcs/Faurbellant.lua (quest
// rework B3b, San d'Oria group 11; call-only: each script now calls this quest). Olbergieut : !pos 91 0 121 231;
// Faurbellant : !pos 484 24 -89 102. Olbergieut's 619 (San d'Oria fame level 2, available; option 0: begun, the
// Scripture of Wind); while accepted his OLBERGIEUT_DIALOG (the Scripture of Wind), spoken to the char. Faurbellant (La
// Theine Plateau), accepted with the Scripture of Wind: FAURBELLANT_2, the Wind taken and the Scripture of Water given;
// without it FAURBELLANT_3; completed FAURBELLANT_4. Olbergieut with the Scripture of Water (in any status): 620, whose
// finish gives the Cotton Cape and, when it fits, completes the quest (the raw player:completeQuest, which keeps the
// quest's own Quest[0][18] vars; fame +20, The Pious One, the Water taken). The scripts keep their own answers when
// the quest does not act: Olbergieut's 612, Faurbellant's FAURBELLANT_1.
// Kept as the Lua has it: 620 plays for the Scripture of Water in any status (a GM-given Water completes an unbegun
// quest, and a completed one again: the cape, fame and title again, the status unchanged); the finishes read no status
// (619's option 0 on a completed quest sets the log's current bit, the status stays completed); the cape is given
// before the completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class GatesToParadise : QuestScript
    {
        public GatesToParadise() : base(QuestLog.Sandoria, "GATES_TO_PARADISE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Olbergieut.lua onTrigger, in its order
            b.AnyState(when: q => q.Player.HasKeyItem(KeyItem.ScriptureOfWater))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Olbergieut).Called().OnTrigger(QuestReply.Event(620));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Olbergieut).Called().OnTrigger(q =>
                {
                    // player:showText(npc, ID.text.OLBERGIEUT_DIALOG, xi.keyItem.SCRIPTURE_OF_WIND)
                    q.Player.ShowText(Text.NorthernSanDoria.OLBERGIEUT_DIALOG, KeyItem.ScriptureOfWind);
                    return QuestReply.NoAction();
                })
                // Faurbellant.lua onTrigger, accepted
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.Faurbellant).Called().OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ScriptureOfWind))
                    {
                        q.Player.ShowText(Text.LaTheinePlateau.FAURBELLANT_2, 0, KeyItem.ScriptureOfWind);
                        q.Player.DelKeyItem(KeyItem.ScriptureOfWind);
                        q.Player.GiveKeyItem(KeyItem.ScriptureOfWater);
                    }
                    else
                    {
                        q.Player.ShowText(Text.LaTheinePlateau.FAURBELLANT_3, KeyItem.ScriptureOfWater);
                    }
                    return QuestReply.NoAction();
                });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 2)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Olbergieut).Called().OnTrigger(QuestReply.Event(619));

            // Faurbellant.lua onTrigger, completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.Faurbellant).Called().OnTrigger(q =>
                {
                    q.Player.ShowText(Text.LaTheinePlateau.FAURBELLANT_4);
                    return QuestReply.NoAction();
                });

            // Olbergieut.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.NorthernSanDoria, 619, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.ScriptureOfWind);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 620, q =>
                {
                    if (q.Player.GiveItem(Item.CottonCape))
                    {
                        // the raw player:completeQuest, which keeps the quest's own Quest[0][18] vars
                        q.Player.CompleteQuest(QuestLog.Sandoria, "GATES_TO_PARADISE");
                        q.Player.AddFame(FameArea.Sandoria, 20);
                        q.Player.AddTitle(Title.ThePiousOne);
                        q.Player.DelKeyItem(KeyItem.ScriptureOfWater);
                    }
                });
        }
    }
}
