// Ghosts of the Past (Log ID 1, Quest ID 51, MNK AF1), a line-for-line port of
// scripts/quests/bastok/MNK_AF1_Ghosts_of_the_Past.lua (quest rework B3b). Oggbi : !pos -159 -7 5 236; qm4 : !pos -174 0 369
// 196. Oggbi offers it (231) to a MNK of AF1_QUEST_LEVEL; a Pickaxe shown to the ??? in Gusgen Mines (not taken) spawns the
// Wandering Ghost (claimed) while the player holds no Miner's Pendant and the ghost is not up; the pendant to Oggbi (232)
// completes it and sets The First Meeting's mustZone. Kept as the Lua file has it: while accepted, any other trade to the
// ??? still shows YOU_CANNOT_EVEN_DIG (the player's message, the handler answering nil).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class GhostsOfThePast : QuestScript
    {
        public GhostsOfThePast() : base(QuestLog.Bastok, "GHOSTS_OF_THE_PAST") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/MNK_AF1_Ghosts_of_the_Past";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Item = Item.BeatCesti });

            b.State(QuestStatus.Available)
                .Npc(Zone.GusgenMines, Npc.GusgenMines.qm4).OnTrade(q =>
                {
                    if (q.Trade.ItemQty(Item.Pickaxe) > 0)
                    {
                        return QuestReply.MessageSpecial(Text.GusgenMines.YOU_CANNOT_EVEN_DIG, Item.Pickaxe);
                    }
                    return null;
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Oggbi).OnTrigger(q =>
                {
                    if (q.Player.MainJob == Job.Mnk &&
                        q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL"))
                    {
                        return QuestReply.ProgressEvent(231);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortBastok, 231, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.GusgenMines, Npc.GusgenMines.qm4).OnTrade(q =>
                {
                    if (q.Trade.ItemQty(Item.Pickaxe) > 0 &&
                        !q.Player.HasItem(Item.MinersPendant) &&
                        !q.Player.MobIsSpawned(Mob.GusgenMines.WANDERING_GHOST))
                    {
                        q.Player.SpawnMob(Mob.GusgenMines.WANDERING_GHOST, 0, claim: true);
                        return QuestReply.MessageSpecial(Text.GusgenMines.YOU_CANNOT_EVEN_DIG, Item.Pickaxe);
                    }
                    else
                    {
                        q.Text.Special(Text.GusgenMines.YOU_CANNOT_EVEN_DIG, Item.Pickaxe);
                        return null;
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Oggbi).OnTrade(q =>
                    q.Trade.Matches((Item.MinersPendant, 1)) ? QuestReply.ProgressEvent(232) : null)
                .OnEventFinish(Zone.PortBastok, 232, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();

                        q.Player.SetQuestMustZone(QuestLog.Bastok, "THE_FIRST_MEETING");
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.GusgenMines, Npc.GusgenMines.qm4).OnTrade(q =>
                {
                    if (q.Trade.ItemQty(Item.Pickaxe) > 0)
                    {
                        return QuestReply.MessageSpecial(Text.GusgenMines.YOU_CANNOT_EVEN_DIG, Item.Pickaxe);
                    }
                    return null;
                });
        }
    }
}
