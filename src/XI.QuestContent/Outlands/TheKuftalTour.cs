// The Kuftal Tour (Log ID 5, Quest ID 195), a line-for-line port of scripts/quests/outlands/The_Kuftal_Tour.lua (quest
// rework B3b Outlands, group 5). Rabao: Datta !pos -24.8 8.3 70.7 (no script; default 73). Kuftal Tunnel: qm5 !pos -29.2
// -22.2 -183.7 (no script; default DO_NOT_SEE_ANYTHING; Bastok 8-2 Enter the Talekeeper's 12 at status 1, a progressEvent:
// the two tie and rotate by the last event id, the mission's first, #mission).
// Offered once The Gustaberg Tour (Bastok) is completed at Selbina-Rabao fame level 3: Datta 74 begins it. Accepted:
// Kuftal Tunnel's qm5: when every alliance member (the char among them) is level 40 or below and within 15 yalms of the
// char, and the party holds more than one, 14, whose finish sets Prog 1; else nil. Datta at Prog 1: 75, whose finish
// runs quest:complete (8000 gil, Kuftal Tourist) and adds San d'Oria and Bastok fame 10 each; else nil.
// Kept as the Lua has it: the fame is added whatever complete returns (gil and a title: it cannot fail); qm5's 14 plays
// again at Prog 1; the alliance's checkDistance is each member's to the char.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TheKuftalTour : QuestScript
    {
        public TheKuftalTour() : base(QuestLog.Outlands, "THE_KUFTAL_TOUR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/The_Kuftal_Tour";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Gil = 8000,
                Title = Title.KuftalTourist,
            });
            b.Steps(1);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "THE_GUSTABERG_TOUR") &&
                    q.Player.FameLevel(FameArea.SelbinaRabao) >= 3)
                .Npc(Zone.Rabao, Npc.Rabao.Datta).OnTrigger(QuestReply.ProgressEvent(74))
                .OnEventFinish(Zone.Rabao, 74, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.KuftalTunnel, Npc.KuftalTunnel.qm5).OnTrigger(q =>
                {
                    bool flag = true;

                    foreach (var member in q.Player.Alliance)
                    {
                        if (member.MainLevel > 40 ||
                            member.Distance > 15)
                        {
                            flag = false;
                        }
                    }

                    if (flag && q.Player.PartySize > 1)
                    {
                        return QuestReply.ProgressEvent(14);
                    }
                    return null;
                })
                .OnEventFinish(Zone.KuftalTunnel, 14, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.Rabao, Npc.Rabao.Datta).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(75);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Rabao, 75, q =>
                {
                    q.Quest.Complete();
                    q.Player.AddFame(FameArea.Sandoria, 10);
                    q.Player.AddFame(FameArea.Bastok, 10);
                });
        }
    }
}
