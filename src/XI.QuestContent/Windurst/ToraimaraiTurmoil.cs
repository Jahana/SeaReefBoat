// Toraimarai Turmoil (Log ID 2, Quest ID 80), a line-for-line port of scripts/quests/windurst/Toraimarai_Turmoil.lua
// (quest rework B3b). Ohbiru-Dohbiru : !pos 23 -5 -193 238; Leepe-Hoppe : !pos -131 20 -174 238; Polikal-Ramikal :
// !pos 15 -18 195 239; Yoran-Oran : !pos -110 -14 203 239. Offered after Blue Ribbon Blues at fame level 6 (785, option
// 1: begun, the Rhinostery Certificate); three Starmite Shells to Ohbiru complete it (791: 4500 gil, fame +16, Certified
// Rhinostery Venturer). Repeatable: once completed, Ohbiru's 795 (option 1: Prog 1) asks again and 791 pays fame +16
// and 4500 gil (giveCurrency) each time. Kept as the Lua file has it: the offer's guard reads the quest's own mustZone,
// which the quest never sets; the certificate is never taken back; the first completion's 791 takes the shells only when
// quest:complete succeeds (it cannot fail: no item reward), and the completed state's 791 then runs too, at Prog 0 (the
// vars cleared): nothing.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class ToraimaraiTurmoil : QuestScript
    {
        public ToraimaraiTurmoil() : base(QuestLog.Windurst, "TORAIMARAI_TURMOIL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Toraimarai_Turmoil";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Gil = 4500,
                Fame = 16,
                FameArea = FameArea.Windurst,
                Title = Title.CertifiedRhinosteryVenturer,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "BLUE_RIBBON_BLUES") &&
                    q.Player.FameLevel(FameArea.Windurst) >= 6 &&
                    !q.Quest.MustZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru)
                    .OnTrigger(QuestReply.ProgressEvent(785, 4500, KeyItem.RhinosteryCertificate, Item.StarmiteShell))
                .OnEventFinish(Zone.WindurstWaters, 785, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.RhinosteryCertificate);
                    }
                });

            // Initial completion
            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly((Item.StarmiteShell, 3)))
                        {
                            return QuestReply.ProgressEvent(791);
                        }

                        return null;
                    })
                    .OnTrigger(q => QuestReply.Event(786, 4500, KeyItem.RhinosteryCertificate, Item.StarmiteShell)) // Reminder text.
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe)
                    .OnTrigger(QuestReply.Event(790, 0, KeyItem.RhinosteryCertificate))
                .OnEventFinish(Zone.WindurstWaters, 791, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                })
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.PolikalRamikal).OnTrigger(QuestReply.Event(391))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.YoranOran).OnTrigger(QuestReply.Event(392));

            // Repeat completion
            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 1 &&
                            q.Trade.HasExactly((Item.StarmiteShell, 3)))
                        {
                            return QuestReply.ProgressEvent(791);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step == 1)
                        {
                            return QuestReply.Event(786, 4500, KeyItem.RhinosteryCertificate, Item.StarmiteShell); // Reminder text.
                        }
                        else
                        {
                            return QuestReply.Event(795, 4500, 0, Item.StarmiteShell); // Repeat dialog.
                        }
                    })
                .OnEventFinish(Zone.WindurstWaters, 791, q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        q.Quest.SetStep(0);
                        q.Trade.Confirm();
                        q.Player.AddFame(FameArea.Windurst, 16);
                        q.Player.GiveCurrency("gil", 4500);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 795, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetStep(1);
                    }
                });
        }
    }
}
