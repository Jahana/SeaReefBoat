// Blade of Darkness (Log ID 1, Quest ID 28), a line-for-line port of scripts/quests/bastok/Blade_of_Darkness.lua (quest
// rework B3b). Gumbah : !pos 52 0 -36 234. Gumbah's 99 (the advanced job level) begins it; the Zeruhn Mines zone-in from
// Palborough Mines plays 130 at Prog 0 (Chaosbringer given, Prog 1) or 131 when the Chaosbringer is not held (given
// again); the Beadeaux zone-in from Pashhow Marshlands with ChaosbringerKills at 100 plays 121, which completes it and
// unlocks Dark Knight. ChaosbringerKills is a char var the quest does not own: scripts/items/chaosbringer.lua counts kills
// while this quest (or Blade of Death) is accepted (a status read, it stays) and clears it when the sword is dropped.
// Kept as the Lua file has it: 130 with a full inventory gives nothing and leaves Prog 0, so the next zone-in plays 130
// again; 131's give is unchecked.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class BladeOfDarkness : QuestScript
    {
        public BladeOfDarkness() : base(QuestLog.Bastok, "BLADE_OF_DARKNESS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Blade_of_Darkness";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Bastok,
                KeyItem = KeyItem.JobGestureDarkKnight,
                Title = Title.DarkSider,
            });

            b.State(QuestStatus.Available, when: q => q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL"))
                .Npc(Zone.BastokMines, Npc.BastokMines.Gumbah).OnTrigger(QuestReply.ProgressEvent(99))
                .OnEventFinish(Zone.BastokMines, 99, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                // prevZone is player:getPreviousZone() (the same value: both are CharData.PrevZone)
                .OnZoneIn(Zone.ZeruhnMines, q =>
                {
                    if (q.Player.PreviousZone == Zone.PalboroughMines)
                    {
                        if (q.Quest.Step == 0)
                        {
                            return QuestReply.Event(130);
                        }
                        else if (!q.Player.HasItem(Item.Chaosbringer))
                        {
                            return QuestReply.Event(131);
                        }
                    }
                    return null;
                })
                .OnEventFinish(Zone.ZeruhnMines, 130, q =>
                {
                    if (q.Player.GiveItem(Item.Chaosbringer))
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.ZeruhnMines, 131, q => q.Player.GiveItem(Item.Chaosbringer))
                .OnZoneIn(Zone.Beadeaux, q =>
                    q.Player.PreviousZone == Zone.PashhowMarshlands &&
                    q.Player.CharVar("ChaosbringerKills") >= 100
                        ? QuestReply.Event(121)
                        : null)
                .OnEventFinish(Zone.Beadeaux, 121, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.UnlockJob(Job.Drk);
                        q.Text.Special(Text.Beadeaux.YOU_CAN_NOW_BECOME_A_DARK_KNIGHT);
                    }
                });
        }
    }
}
