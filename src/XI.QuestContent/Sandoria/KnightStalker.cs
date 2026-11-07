// Knight Stalker (Log ID 0, Quest ID 96, DRG AF3), ported from the quest branches of
// scripts/zones/Chateau_dOraguille/npcs/Rahal.lua, scripts/zones/Port_San_dOria/npcs/Ceraulian.lua,
// scripts/zones/Southern_San_dOria/npcs/Balasiel.lua and scripts/zones/Temple_of_Uggalepih/npcs/qm15.lua
// (quest rework B3b, San d'Oria group 15; call-only). Rahal : !pos -28 0.1 -6 233; Ceraulian : !pos 0 -8 -122 232;
// Balasiel : !pos -136 -11 64 230 (his default 667); qm15 (???) : !pos 58 1 -70 159.
// Rahal, after his two Holy Crest arms (Lua): Chasing Quotas completed, this quest available and a DRG: 121 while the
// char var KnightStalker_Declined is 0, else 120 (the short version); accepted: KnightStalker_Progress 0: 119; with the
// Challenge to the Royal Knights: Progress 1: 78, 2: 69, 3: 110; any other case of the accepted arm plays nothing at
// all (the arm taken: not his 118 / 529). 121 with option 1: begun; with another option: Declined 1. 120 with option 1:
// begun, Declined 0. 78: Progress 2. 110: Progress 4.
// Ceraulian, after The Holy Crest's arm: Chasing Quotas completed and available: 16 (fluff); accepted and
// Progress 0: 19 (finish: Progress 1); Progress 1 (any status): 23 without the challenge, 20 with it; the char var
// KnightStalker_Option1 1: 22 (finish: Option1 0); completed: 21. Else his script's 587.
// Balasiel: accepted and Progress 2: 63 (finish: Progress 3).
// qm15, after its pop arm (Lua): KnightStalker_Kill 1: 67, whose finish is npcUtil.completeQuest (the Drachen Armet,
// The Paragon of Dragoon Excellence, KnightStalker_Kill and KnightStalker_Progress cleared, completed); completed: the
// challenge taken, Option1 1, Option2 1 (the optional cutscenes). Else its script's SOME_SORT_OF_CEREMONY.
// npcUtil.completeQuest's table names two vars, which QuestReward.Var (one) cannot say, and quest:complete's cleanup
// would clear the quest's own Quest[0][96] vars: the finish makes the calls npcUtil.completeQuest makes, in its order
// (npcUtil.giveItem, stopping on a failure; the title; the two vars; then the raw player:completeQuest), and no
// QuestReward is declared.
// Stay Lua (char var / key item only, they never name the quest): the Kuftal Tunnel Treasure Coffer (Progress 1 without
// the challenge: the challenge as the chest's reward), qm15's pop arm (Progress 4, Kill 0, a DRG with its wyvern out:
// Cleuvarion M Resoaix and Rompaulion S Citalle popped, SOME_SORT_OF_CEREMONY + 1), the two NMs' deaths (Progress 4 and
// the other one dead: Kill 1), Rahal's 118 (Option2 1; finish: Option2 0). Read only: roe_records, Vingijard.
// Kept as the Lua has it: npcUtil.completeQuest's fame = 20 has no fameArea, so no fame is given; Rahal's accepted arm
// plays nothing for a Progress the sub-arms miss; Ceraulian's Progress 1 arm reads no
// status; 121 / 120 / 78 / 110 / 19 / 22 / 63 / 67's finishes read no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class KnightStalker : QuestScript
    {
        const string Progress = "KnightStalker_Progress";
        const string Declined = "KnightStalker_Declined";
        const string Kill = "KnightStalker_Kill";
        const string Option1 = "KnightStalker_Option1";
        const string Option2 = "KnightStalker_Option2";

        public KnightStalker() : base(QuestLog.Sandoria, "KNIGHT_STALKER") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                // Rahal.lua onTrigger: the Knight Stalker arms (after his Holy Crest arms; his 118 and 529 after them)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Rahal).Called().OnTrigger(q =>
                {
                    QuestStatus status = q.Quest.Status;
                    int progress = q.Player.CharVar(Progress);

                    // completed AF2, AF3 available, and on DRG (no level check: AF2 is cleared)
                    if (q.Player.QuestStatus(QuestLog.Sandoria, "CHASING_QUOTAS") == QuestStatus.Completed &&
                        status == QuestStatus.Available &&
                        q.Player.MainJob == Job.Drg)
                    {
                        if (q.Player.CharVar(Declined) == 0)
                        {
                            return QuestReply.Event(121); // start AF3
                        }
                        else
                        {
                            return QuestReply.Event(120); // the short version after a decline
                        }
                    }
                    else if (status == QuestStatus.Accepted)
                    {
                        if (progress == 0)
                        {
                            return QuestReply.Event(119); // go to Brugaire / Ceraulian
                        }
                        else if (q.Player.HasKeyItem(KeyItem.ChallengeToTheRoyalKnights))
                        {
                            if (progress == 1)
                            {
                                return QuestReply.Event(78); // the challenge: go talk to Balasiel
                            }
                            else if (progress == 2)
                            {
                                return QuestReply.Event(69); // reminder: Balasiel
                            }
                            else if (progress == 3)
                            {
                                return QuestReply.Event(110); // to the south with you
                            }
                        }

                        return QuestReply.NoAction(); // the arm taken, no event: his 118 / 529 not played
                    }

                    return null;
                })
                // Ceraulian.lua onTrigger: the Knight Stalker arms (after The Holy Crest's; his 587 after them)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Ceraulian).Called().OnTrigger(q =>
                {
                    QuestStatus status = q.Quest.Status;
                    int progress = q.Player.CharVar(Progress);

                    if (q.Player.QuestStatus(QuestLog.Sandoria, "CHASING_QUOTAS") == QuestStatus.Completed &&
                        status == QuestStatus.Available)
                    {
                        return QuestReply.Event(16); // fluff until DRG AF3
                    }
                    else if (status == QuestStatus.Accepted && progress == 0)
                    {
                        return QuestReply.Event(19); // fetch the last dragoon's helmet
                    }
                    else if (progress == 1)
                    {
                        if (!q.Player.HasKeyItem(KeyItem.ChallengeToTheRoyalKnights))
                        {
                            return QuestReply.Event(23); // reminder: the helmet
                        }
                        else
                        {
                            return QuestReply.Event(20); // the challenge shown to Ceraulian
                        }
                    }
                    else if (q.Player.CharVar(Option1) == 1)
                    {
                        return QuestReply.Event(22);
                    }
                    else if (status == QuestStatus.Completed)
                    {
                        return QuestReply.Event(21);
                    }

                    return null;
                })
                // Balasiel.lua onTrigger
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Balasiel).Called().OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted &&
                        q.Player.CharVar(Progress) == 2)
                    {
                        return QuestReply.Event(63); // DRG AF3 cutscene
                    }

                    return null;
                })
                // qm15.lua onTrigger: the 67 arm (after its pop arm; its SOME_SORT_OF_CEREMONY after it)
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.qm15).Called().OnTrigger(q =>
                {
                    if (q.Player.CharVar(Kill) == 1)
                    {
                        return QuestReply.Event(67);
                    }

                    return null;
                });

            b.AnyState()
                // Rahal.lua onEventFinish 121, 120, 78, 110
                .OnEventFinish(Zone.ChateauDoraguille, 121, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Player.SetCharVar(Declined, 1);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 120, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.SetCharVar(Declined, 0);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 78, q =>
                {
                    q.Player.SetCharVar(Progress, 2);
                })
                .OnEventFinish(Zone.ChateauDoraguille, 110, q =>
                {
                    q.Player.SetCharVar(Progress, 4);
                })
                // Ceraulian.lua onEventFinish 19, 22
                .OnEventFinish(Zone.PortSanDoria, 19, q =>
                {
                    q.Player.SetCharVar(Progress, 1);
                })
                .OnEventFinish(Zone.PortSanDoria, 22, q =>
                {
                    q.Player.SetCharVar(Option1, 0);
                })
                // Balasiel.lua onEventFinish 63
                .OnEventFinish(Zone.SouthernSanDoria, 63, q =>
                {
                    q.Player.SetCharVar(Progress, 3);
                })
                // qm15.lua onEventFinish 67
                .OnEventFinish(Zone.TempleOfUggalepih, 67, q =>
                {
                    // npcUtil.completeQuest(player, SANDORIA, KNIGHT_STALKER, { item = 12519, fame = 20,
                    // title = PARAGON_OF_DRAGOON_EXCELLENCE,
                    // var = { 'KnightStalker_Kill', 'KnightStalker_Progress' } }), step for step (fame without a
                    // fameArea: none)
                    if (!q.Player.GiveItem(Item.DrachenArmet))
                    {
                        return; // no free slot: npcUtil.completeQuest returns false
                    }

                    q.Player.AddTitle(Title.ParagonOfDragoonExcellence);
                    q.Player.SetCharVar(Kill, 0);
                    q.Player.SetCharVar(Progress, 0);
                    q.Player.CompleteQuest(QuestLog.Sandoria, "KNIGHT_STALKER"); // the raw player:completeQuest

                    q.Player.DelKeyItem(KeyItem.ChallengeToTheRoyalKnights);
                    q.Player.SetCharVar(Option1, 1); // optional post-quest cutscenes
                    q.Player.SetCharVar(Option2, 1);
                });
        }
    }
}
