// Dark Puppet (Log ID 1, Quest ID 58), a line-for-line port of scripts/quests/bastok/DRK_AF2_Dark_Puppet.lua (quest
// rework B3b). Cid : !pos -12 -12 1 237; qm4 (Axe) : !pos -52 27 -85 193; qm5 (Sword) : !pos -92 -28 -70 193; qm6 (Soul) :
// !pos -132 -27 -245 193. Cid's 760 (Dark Legacy completed, DRK, AF2 level), the Ordelle's Caves zone-in 10 (Prog 1), a
// Darksteel Ingot at qm4 (Gerwitz's Axe, DARK_PUPPET_OFFSET), Gerwitz's Axe at qm5 (Gerwitz's Sword, +1), Gerwitz's
// Sword at qm6 (Gerwitz's Soul, +2), the Soul's death (Prog 2), the La Theine Plateau zone-in 122 and the reward.
// Kept as the Lua file has it: the qm pops use hide = 0 (the ??? stays shown); the Soul's death sets Prog 2 at any Prog;
// the Prog 0 check is on the zone-in only, so a Prog 1 char pops the mobs as well as a Prog 2 one; qm6 has no "already
// held" guard.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class DarkPuppet : QuestScript
    {
        public DarkPuppet() : base(QuestLog.Bastok, "DARK_PUPPET") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/DRK_AF2_Dark_Puppet";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Item = Item.ChaosSollerets });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "DARK_LEGACY") &&
                    q.Player.MainJob == Job.Drk &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL"))
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(QuestReply.ProgressEvent(760))
                .OnEventFinish(Zone.Metalworks, 760, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.OrdellesCaves, Npc.OrdellesCaves.qm4).OnTrade(q =>
                {
                    if (!q.Player.HasItem(Item.GerwitzsAxe) &&
                        q.Trade.Matches((Item.DarksteelIngot, 1)) &&
                        q.Player.PopFromQm(Mob.OrdellesCaves.DARK_PUPPET_OFFSET, hide: 0))
                    {
                        q.Trade.Complete();

                        return QuestReply.MessageSpecial(Text.OrdellesCaves.SENSE_A_FOUL_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.OrdellesCaves, Npc.OrdellesCaves.qm5).OnTrade(q =>
                {
                    if (!q.Player.HasItem(Item.GerwitzsSword) &&
                        q.Trade.Matches((Item.GerwitzsAxe, 1)) &&
                        q.Player.PopFromQm(Mob.OrdellesCaves.DARK_PUPPET_OFFSET, hide: 0, offset: 1))
                    {
                        q.Trade.Complete();

                        return QuestReply.MessageSpecial(Text.OrdellesCaves.SENSE_A_FOUL_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.OrdellesCaves, Npc.OrdellesCaves.qm6).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.GerwitzsSword, 1)) &&
                        q.Player.PopFromQm(Mob.OrdellesCaves.DARK_PUPPET_OFFSET, hide: 0, offset: 2))
                    {
                        q.Trade.Complete();

                        return QuestReply.MessageSpecial(Text.OrdellesCaves.SENSE_A_FOUL_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.OrdellesCaves, Npc.OrdellesCaves.GerwitzsSoul).OnMobDeath(q => q.Quest.SetStep(2))
                .OnZoneIn(Zone.OrdellesCaves, q => q.Quest.Step == 0 ? QuestReply.Event(10) : null)
                .OnEventFinish(Zone.OrdellesCaves, 10, q => q.Quest.SetStep(1))
                .OnZoneIn(Zone.LaTheinePlateau, q => q.Quest.Step == 2 ? QuestReply.Event(122) : null)
                .OnEventFinish(Zone.LaTheinePlateau, 122, q => q.Quest.Complete());
        }
    }
}
