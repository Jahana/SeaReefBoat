// A Question of Taste (Log ID 5, Quest ID 3), a line-for-line port of scripts/quests/outlands/A_Question_of_Taste.lua
// (quest rework B3b Outlands, group 2). Kazham: Etteh Sulaej !pos 94 -14.5 -111 (no script; default 42), Jakoh Wahcondalo
// !pos 101 -15 -115 (his script: Tuning Out only; default 113). Windurst Waters: Angelica !pos -64 -9.25 -9 (her script
// cycles 86 / 87). Temple of Uggalepih: Stone_Picture_Frame_QoT !pos 80 -2 -39.6 (no script; default FRAME_FOR_A_PAINTING),
// the NM Trompe L'Oeil.
// Offered at Windurst fame level 6: Etteh 44 (a progress cutscene) begins it and gives the Letter to Angelica. Accepted:
// Angelica 771 (Prog 0, A Pose By Any Other Name completed and its mustZone clear) sets Prog 1, gives Final Fantasy and
// Angelica's Letter and takes the Letter to Angelica (772 after); Etteh 47 at Prog 1 (Prog 2, Angelica's Letter taken), 49
// from 2, 45 at 0; Jakoh 48 from Prog 2, else 46. The Temple's frame at Prog 2: while Wait is ahead FRAME_FOR_A_PAINTING + 1
// and STILL_HANGS_ON_THE_WALL, else with Trompe L'Oeil not spawned 50 (option 1: Final Fantasy taken, the NM spawned and
// claimed, Wait now + 900); his death at Prog 2 sets Prog 3; the frame at Prog 3 gives the Ripped Final Fantasy Painting
// (Prog 4, no action); Etteh 50 at Prog 4 completes it (3000 gil), then Windurst fame 16, the painting taken, the mustZone.
// Completed (the repeat, the same vars): Etteh 51 / Jakoh 52 while the mustZone is set; Etteh answers nil (the default)
// while the char meets Everyone's Grudging's offer (rancorCurse 1, fame level 7, Everyone's Grudging not completed); else
// 57 at Prog 3, 55 from Prog 1, at Prog 0 53 (Option 0) or 54: 53's option 2 sets Option 1, any other begins (Prog 1, Final
// Fantasy Part II); 54's option 1 begins. The frame at Prog 1 plays 51 (the same spawn), the death at Prog 1 sets Prog 2,
// the frame at Prog 2 gives the painting (Prog 3); Etteh 57 completes again. Angelica 772 at Prog 1, else 773
// (replaceDefault). Jakoh 56 from Prog 1.
// Kept as the Lua has it: 53 begins on every option but 2 (0 included); quest:begin on the completed quest keeps it
// completed (addQuest); the frame's nil while Trompe L'Oeil is up (the default); the message before the reply in the Wait
// arm; complete's cleanup clears Prog / Option / Wait before the mustZone is set; Etteh 50 (Kazham) and the frame's 50
// (Temple) are two events of one number.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class AQuestionOfTaste : QuestScript
    {
        public AQuestionOfTaste() : base(QuestLog.Outlands, "A_QUESTION_OF_TASTE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/A_Question_of_Taste";

        // the frame's Wait arm: player:messageSpecial(templeID.text.FRAME_FOR_A_PAINTING + 1), then the quest's message
        static QuestReply StillHangs(IQuestContext q)
        {
            q.Text.Special(q.Text.Id(Text.TempleOfUggalepih.FRAME_FOR_A_PAINTING) + 1);
            return QuestReply.MessageSpecial(Text.TempleOfUggalepih.STILL_HANGS_ON_THE_WALL, KeyItem.RippedFinalFantasyPainting);
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 3000 });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Windurst) >= 6)
                .Npc(Zone.Kazham, Npc.Kazham.EttehSulaej).OnTrigger(QuestReply.ProgressCutscene(44))
                .OnEventFinish(Zone.Kazham, 44, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.LetterToAngelica);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Kazham, Npc.Kazham.EttehSulaej).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 1)
                    {
                        return QuestReply.ProgressEvent(47);
                    }
                    else if (progress == 4)
                    {
                        return QuestReply.ProgressEvent(50);
                    }
                    else if (progress >= 2)
                    {
                        return QuestReply.Event(49);
                    }
                    else
                    {
                        return QuestReply.Event(45);
                    }
                })
                .Npc(Zone.Kazham, Npc.Kazham.JakohWahcondalo).OnTrigger(q =>
                {
                    if (q.Quest.Step >= 2)
                    {
                        return QuestReply.Event(48);
                    }
                    else
                    {
                        return QuestReply.Event(46);
                    }
                })
                .OnEventFinish(Zone.Kazham, 47, q =>
                {
                    q.Quest.SetStep(2);
                    q.Player.DelKeyItem(KeyItem.AngelicasLetter);
                })
                .OnEventFinish(Zone.Kazham, 50, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Windurst, 16);
                        q.Player.DelKeyItem(KeyItem.RippedFinalFantasyPainting);
                        q.Quest.SetMustZone();
                    }
                })
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.StonePictureFrameQoT).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 2)
                    {
                        if (q.Quest.Var("Wait") > q.Player.SystemTime)
                        {
                            return StillHangs(q);
                        }
                        // Lua: elseif not questNM then return: GetMobByID of the zone's own TROMPE_LOEIL is never nil
                        else if (!q.Player.MobIsSpawned(Mob.TempleOfUggalepih.TROMPE_LOEIL))
                        {
                            return QuestReply.ProgressEvent(50, KeyItem.FinalFantasy);
                        }
                    }
                    else if (progress == 3)
                    {
                        q.Player.GiveKeyItem(KeyItem.RippedFinalFantasyPainting);
                        q.Quest.SetStep(4);
                        return QuestReply.NoAction();
                    }
                    return null;
                })
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.TrompeLOeil).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        q.Quest.SetStep(3);
                    }
                })
                .OnEventFinish(Zone.TempleOfUggalepih, 50, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelKeyItem(KeyItem.FinalFantasy);
                        // SpawnMob(templeID.mob.TROMPE_LOEIL):updateClaim(player)
                        q.Player.SpawnMob(Mob.TempleOfUggalepih.TROMPE_LOEIL, 0, claim: true);
                        q.Quest.SetVar("Wait", q.Player.SystemTime + 900); // Sets a 15min cooldown before the player can repop the NM.
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Angelica).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (q.Player.HasCompletedQuest(QuestLog.Windurst, "A_POSE_BY_ANY_OTHER_NAME") &&
                        !q.Player.QuestMustZone(QuestLog.Windurst, "A_POSE_BY_ANY_OTHER_NAME"))
                    {
                        if (progress == 0)
                        {
                            return QuestReply.ProgressEvent(771);
                        }
                        else
                        {
                            return QuestReply.Event(772);
                        }
                    }
                    return null;
                })
                .OnEventFinish(Zone.WindurstWaters, 771, q =>
                {
                    q.Quest.SetStep(1);
                    q.Player.GiveKeyItem(KeyItem.FinalFantasy);
                    q.Player.GiveKeyItem(KeyItem.AngelicasLetter);
                    q.Player.DelKeyItem(KeyItem.LetterToAngelica);
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Kazham, Npc.Kazham.EttehSulaej).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (q.Quest.MustZone)
                    {
                        return QuestReply.Event(51);
                    }
                    else if (
                        // The player cannot do the repeat if they meet the requirements for "Everyone's Grudging"
                        q.Player.CharVar("rancorCurse") == 1 &&
                        q.Player.FameLevel(FameArea.Windurst) >= 7 &&
                        !q.Player.HasCompletedQuest(QuestLog.Outlands, "EVERYONES_GRUDGING"))
                    {
                        return null; // Sends to default action as seen in captures
                    }
                    else
                    {
                        if (progress == 3)
                        {
                            return QuestReply.ProgressEvent(57);
                        }
                        else if (progress >= 1)
                        {
                            return QuestReply.Event(55);
                        }
                        else
                        {
                            if (q.Quest.Var("Option") == 0)
                            {
                                return QuestReply.ProgressEvent(53);
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(54);
                            }
                        }
                    }
                })
                .Npc(Zone.Kazham, Npc.Kazham.JakohWahcondalo).OnTrigger(q =>
                {
                    if (q.Quest.MustZone)
                    {
                        return QuestReply.Event(52);
                    }
                    else if (q.Quest.Step >= 1)
                    {
                        return QuestReply.Event(56);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Kazham, 53, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Quest.SetVar("Option", 1);
                    }
                    else
                    {
                        q.Quest.SetStep(1);
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.FinalFantasyPartIi);
                    }
                })
                .OnEventFinish(Zone.Kazham, 54, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetStep(1);
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.FinalFantasyPartIi);
                    }
                })
                .OnEventFinish(Zone.Kazham, 57, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Windurst, 16);
                        q.Player.DelKeyItem(KeyItem.RippedFinalFantasyPainting);
                        q.Quest.SetMustZone();
                    }
                })
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.StonePictureFrameQoT).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 1)
                    {
                        if (q.Quest.Var("Wait") > q.Player.SystemTime)
                        {
                            return StillHangs(q);
                        }
                        // Lua: elseif not questNM then return (never nil: the zone's own mob)
                        else if (!q.Player.MobIsSpawned(Mob.TempleOfUggalepih.TROMPE_LOEIL))
                        {
                            return QuestReply.ProgressEvent(51, KeyItem.FinalFantasyPartIi);
                        }
                    }
                    else if (progress == 2)
                    {
                        q.Player.GiveKeyItem(KeyItem.RippedFinalFantasyPainting);
                        q.Quest.SetStep(3);
                        return QuestReply.NoAction();
                    }
                    return null;
                })
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.TrompeLOeil).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        q.Quest.SetStep(2);
                    }
                })
                .OnEventFinish(Zone.TempleOfUggalepih, 51, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelKeyItem(KeyItem.FinalFantasyPartIi);
                        q.Player.SpawnMob(Mob.TempleOfUggalepih.TROMPE_LOEIL, 0, claim: true);
                        q.Quest.SetVar("Wait", q.Player.SystemTime + 900); // Sets a 15min cooldown before the player can repop the NM.
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Angelica).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.Event(772);
                    }
                    else
                    {
                        return QuestReply.Event(773).ReplaceDefault();
                    }
                });
        }
    }
}
