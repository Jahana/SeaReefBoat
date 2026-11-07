// Soothing Waters (Log ID 6, Quest ID 47), a line-for-line port of scripts/quests/ahtUrhgan/Soothing_Waters.lua (quest
// rework B3b Aht Urhgan group 6). Aht Urhgan Whitegate: Fari-Wari !pos 80 -6 -137 (no script; default event 831),
// Eunheem !pos -56 0 -3 (no script, no default), Nadeey !pos 80 0 55 (no script; default 251; ToAU 4-7 and RoV 2-10
// answer there too). Al Zahbi: Mihli Aliapoh, Rughadjeen, Gadalar, Zazarg, Najelith (no scripts;
// defaults 267, 265, 266, 268, 269). Aydeewa Subterrane: qm10 !pos 352 1.8 376.8 (no script;
// default NOTHING_OUT_OF_ORDINARY).
// Available with Ode to the Serpents completed: Fari-Wari 894, option 0: begun. Accepted: Prog 0: Fari-Wari 906
// (replaceEvent), Eunheem 895 (Prog 1); Prog 1: Fari-Wari 906, Eunheem 907, Nadeey 896 (Prog 2); Prog 2: Fari-Wari 906,
// Eunheem 907, Mihli Aliapoh 289 (Prog 3); Prog 3: Fari-Wari 906, Eunheem 907, a trade of exactly one Tuft of Colorful
// Hair to Aydeewa's qm10: the cutscene 34 (Prog 4, the tuft taken); Prog 4: Fari-Wari 897, Eunheem 907; 897's finish
// completes it (an Imperial Gold Piece, Springserpent Sentry) and, when it does, 500 imperial standing and
// BESIEGED_OFFSET. Completed: Eunheem 285, Rughadjeen 284 ([0] the mercenary rank), Gadalar 285, Mihli Aliapoh 286,
// Zazarg 287, Najelith 288 (each replaceEvent).
// Kept as the Lua has it: Eunheem's completed reply is 285 in Whitegate (Gadalar's id in Al Zahbi); 34's finish sets
// Prog 4 before it takes the tuft.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class SoothingWaters : QuestScript
    {
        public SoothingWaters() : base(QuestLog.AhtUrghan, "SOOTHING_WATERS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Soothing_Waters";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ImperialGoldPiece,
                Title = Title.SpringserpentSentry,
            });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "ODE_TO_THE_SERPENTS"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari)
                    .OnTrigger(QuestReply.ProgressEvent(894).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 894, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari).OnTrigger(QuestReply.ReplaceEvent(906))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Eunheem)
                    .OnTrigger(QuestReply.ProgressEvent(895).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 895, q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 1)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari).OnTrigger(QuestReply.ReplaceEvent(906))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Eunheem).OnTrigger(QuestReply.ReplaceEvent(907))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Nadeey)
                    .OnTrigger(QuestReply.ProgressEvent(896).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 896, q =>
                {
                    q.Quest.SetStep(2);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 2)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari).OnTrigger(QuestReply.ReplaceEvent(906))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Eunheem).OnTrigger(QuestReply.ReplaceEvent(907))
                .Npc(Zone.AlZahbi, Npc.AlZahbi.MihliAliapoh).OnTrigger(QuestReply.ProgressEvent(289))
                .OnEventFinish(Zone.AlZahbi, 289, q =>
                {
                    q.Quest.SetStep(3);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 3)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari).OnTrigger(QuestReply.ReplaceEvent(906))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Eunheem).OnTrigger(QuestReply.ReplaceEvent(907))
                .Npc(Zone.AydeewaSubterrane, Npc.AydeewaSubterrane.qm10).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.TuftOfColorfulHair))
                    {
                        return QuestReply.ProgressCutscene(34);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AydeewaSubterrane, 34, q =>
                {
                    q.Quest.SetStep(4);
                    q.Trade.Confirm();
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 4)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari)
                    .OnTrigger(QuestReply.ProgressEvent(897).TextTable(0))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Eunheem).OnTrigger(QuestReply.ReplaceEvent(907))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 897, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddCurrency("imperial_standing", 500);
                        // zones[player:getZoneID()].text.BESIEGED_OFFSET: the handler's zone, Whitegate
                        q.Text.Special(Text.AhtUrhganWhitegate.BESIEGED_OFFSET);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Eunheem).OnTrigger(QuestReply.ReplaceEvent(285))
                .Npc(Zone.AlZahbi, Npc.AlZahbi.Rughadjeen)
                    .OnTrigger(q => QuestReply.ReplaceEvent(284).Option(0, q.Player.MercenaryRank))
                .Npc(Zone.AlZahbi, Npc.AlZahbi.Gadalar).OnTrigger(QuestReply.ReplaceEvent(285))
                .Npc(Zone.AlZahbi, Npc.AlZahbi.MihliAliapoh).OnTrigger(QuestReply.ReplaceEvent(286))
                .Npc(Zone.AlZahbi, Npc.AlZahbi.Zazarg).OnTrigger(QuestReply.ReplaceEvent(287))
                .Npc(Zone.AlZahbi, Npc.AlZahbi.Najelith).OnTrigger(QuestReply.ReplaceEvent(288));
        }
    }
}
