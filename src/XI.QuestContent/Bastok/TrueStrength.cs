// True Strength (Log ID 1, Quest ID 53, MNK AF3), a line-for-line port of scripts/quests/bastok/MNK_AF3_True_Strength.lua
// (quest rework B3b). Ayame : !pos 133 -19 34 237; qm_truestrength : !pos -100 -71 -132 151. Ayame offers it (748) once The
// First Meeting is completed; the Bottle of Yagudo Drink to the ??? in Castle Oztroja, with no Xalmo Feather held, pops Huu
// Xalmo the Savage (npcUtil.popFromQM, hide 0) and takes the drink; the feather to Ayame (749) completes it.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TrueStrength : QuestScript
    {
        public TrueStrength() : base(QuestLog.Bastok, "TRUE_STRENGTH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/MNK_AF3_True_Strength";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Bastok,
                Item = Item.TempleHose,
                Title = Title.ParagonOfMonkExcellence,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "THE_FIRST_MEETING") &&
                    q.Player.MainJob == Job.Mnk &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF3_QUEST_LEVEL"))
                .Npc(Zone.Metalworks, Npc.Metalworks.Ayame).OnTrigger(QuestReply.ProgressEvent(748))
                .OnEventFinish(Zone.Metalworks, 748, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.CastleOztroja, Npc.CastleOztroja.qmtruestrength).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.BottleOfYagudoDrink, 1)) &&
                        !q.Player.HasItem(Item.XalmoFeather) &&
                        q.Player.PopFromQm(Mob.CastleOztroja.HUU_XALMO_THE_SAVAGE, claim: true, hide: 0))
                    {
                        q.Trade.Complete();

                        return QuestReply.MessageSpecial(Text.CastleOztroja.YOU_HOLD, Item.BottleOfYagudoDrink);
                    }
                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Ayame).OnTrade(q =>
                    q.Trade.Matches((Item.XalmoFeather, 1)) ? QuestReply.ProgressEvent(749) : null)
                .OnEventFinish(Zone.Metalworks, 749, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
