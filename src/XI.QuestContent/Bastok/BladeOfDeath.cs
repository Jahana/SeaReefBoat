// Blade of Death (Log ID 1, Quest ID 47), a line-for-line port of scripts/quests/bastok/Blade_of_Death.lua (quest
// rework B3b). Gumbah : !pos 52 0 -36 234; qm2 : !pos 206 -60 -101 196. Gumbah's 130 (Blade of Darkness completed, Bastok
// fame level 3) begins it with the Letter from Zeid; the Zeruhn Mines zone-in from Palborough Mines replaces a lost
// Chaosbringer (131); the Chaosbringer traded at Gusgen Mines' qm2 with ChaosbringerKills at 200 plays 10, which completes
// it (Deathbringer), takes the sword and deletes the letter. ChaosbringerKills is scripts/items/chaosbringer.lua's char var
// (a status read there; it stays): its onItemDrop clears it when the sword leaves the inventory, the 10 finish's
// tradeComplete included.
// Kept as the Lua file has it: 131's give is unchecked; the quest itself does not clear ChaosbringerKills.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class BladeOfDeath : QuestScript
    {
        public BladeOfDeath() : base(QuestLog.Bastok, "BLADE_OF_DEATH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Blade_of_Death";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 30,
                FameArea = FameArea.Bastok,
                Item = Item.Deathbringer,
                Title = Title.BlackDeath,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "BLADE_OF_DARKNESS") &&
                    q.Player.FameLevel(FameArea.Bastok) >= 3)
                .Npc(Zone.BastokMines, Npc.BastokMines.Gumbah).OnTrigger(QuestReply.ProgressEvent(130))
                .OnEventFinish(Zone.BastokMines, 130, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.LetterFromZeid);
                });

            b.State(QuestStatus.Accepted)
                // prevZone is player:getPreviousZone() (the same value: both are CharData.PrevZone)
                .OnZoneIn(Zone.ZeruhnMines, q =>
                    q.Player.PreviousZone == Zone.PalboroughMines && !q.Player.HasItem(Item.Chaosbringer)
                        ? QuestReply.Event(131)
                        : null)
                .OnEventFinish(Zone.ZeruhnMines, 131, q => q.Player.GiveItem(Item.Chaosbringer))
                .Npc(Zone.GusgenMines, Npc.GusgenMines.qm2).OnTrade(q =>
                    q.Player.CharVar("ChaosbringerKills") >= 200 && q.Trade.Matches((Item.Chaosbringer, 1))
                        ? QuestReply.ProgressEvent(10)
                        : null)
                .OnEventFinish(Zone.GusgenMines, 10, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.DelKeyItem(KeyItem.LetterFromZeid);
                    }
                });
        }
    }
}
