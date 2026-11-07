// Curses, Foiled A-Golem!? (Log ID 2, Quest ID 63), a line-for-line port of
// scripts/quests/windurst/Curses_Foiled_A_Golem.lua (quest rework B3b). Shantotto : !pos 122 -2 112 239;
// Torino-Samarino : !pos 105 -20 140 111; Cermet Door : !pos -183 0 190 204.
// After Curses, Foiled Again!? (2) at fame level 4, level 10 and a zone change: Shantotto's 340 (option 1: begun; else
// the title Total Loser). Torino-Samarino in Beaucedine Glacier gives Shantotto's New Spell (104, option 1: Prog 1); the
// Cermet Door (_no4) in Fei'Yin takes it (14: Prog 2); a kill of one of eight Fei'Yin mob families while holding it
// turns it into Shantotto's Ex-Spell, which Torino-Samarino takes back (108: a one-day wait) and replaces (109).
// Shantotto's 342 at Prog 2 completes the quest (Total Loser, fame +40, Scroll of Warp II, the flavor-of-the-month title).
// Kept as the Lua file has it: the accepted section's 'Leigon-Moigon' and 'Potete' entries (Beaucedine Glacier NPCs)
// sit outside any zone table, so the interaction lookup files them under a zone id that is a name and never reaches
// them (interaction_lookup.lua addContainer); they are left out here, which keeps their behaviour (none). Also kept:
// 340's refusal and 342 give Total Loser by setTitle / addTitle as the Lua has them, and 342 gives it before
// quest:complete, so a full bag still gets the title.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class CursesFoiledAGolem : QuestScript
    {
        public CursesFoiledAGolem() : base(QuestLog.Windurst, "CURSES_FOILED_A_GOLEM") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Curses_Foiled_A_Golem";

        // Block used by mobs which can remove "Shantotto's New Spell" key item.  This needs
        // verification regarding it being all mobs or if limited to current implementation,
        // and also if there is a chance or limit for kills before removal.
        static void FeiyinMobDeath(IQuestContext q)
        {
            if (q.Player.HasKeyItem(KeyItem.ShantottosNewSpell))
            {
                q.Player.DelKeyItem(KeyItem.ShantottosNewSpell);
                q.Player.GiveKeyItem(KeyItem.ShantottosExSpell);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 40,
                FameArea = FameArea.Windurst,
                Item = Item.ScrollOfWarpIi,
                Title = Title.DoctorShantottosFlavorOfTheMonth,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "CURSES_FOILED_AGAIN_2") &&
                    q.Player.FameLevel(FameArea.Windurst) >= 4 &&
                    q.Player.MainLevel >= 10 &&
                    !q.Quest.MustZone)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Shantotto).OnTrigger(QuestReply.ProgressEvent(340))
                .OnEventFinish(Zone.WindurstWalls, 340, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Player.SetTitle(Title.TotalLoser);
                    }
                });

            // ['Leigon-Moigon'] and ['Potete'] (Lua lines 66-86) are keyed outside any zone table: never looked up.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.BeaucedineGlacier, Npc.BeaucedineGlacier.TorinoSamarino).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(104);
                    }
                    else if (questProgress == 1)
                    {
                        if (q.Player.HasKeyItem(KeyItem.ShantottosNewSpell))
                        {
                            return QuestReply.Event(105);
                        }
                        else if (q.Player.HasKeyItem(KeyItem.ShantottosExSpell))
                        {
                            return QuestReply.ProgressEvent(108);
                        }
                        else
                        {
                            if (q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                            {
                                return QuestReply.ProgressEvent(109);
                            }
                            else
                            {
                                return QuestReply.Event(113);
                            }
                        }
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(110);
                    }

                    return null;
                })
                .OnEventFinish(Zone.BeaucedineGlacier, 104, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.ShantottosNewSpell);
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.BeaucedineGlacier, 108, q =>
                {
                    q.Player.DelKeyItem(KeyItem.ShantottosExSpell);
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                })
                .OnEventFinish(Zone.BeaucedineGlacier, 109, q => q.Player.GiveKeyItem(KeyItem.ShantottosNewSpell))
                .Npc(Zone.Feiyin, Npc.Feiyin.no4).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ShantottosNewSpell))
                    {
                        return QuestReply.ProgressEvent(14);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.ShantottosExSpell))
                    {
                        return QuestReply.Event(13);
                    }

                    return null;
                })
                .Npc(Zone.Feiyin, Npc.Feiyin.ClockworkPod).OnMobDeath(FeiyinMobDeath)
                .Npc(Zone.Feiyin, Npc.Feiyin.Colossus).OnMobDeath(FeiyinMobDeath)
                .Npc(Zone.Feiyin, Npc.Feiyin.Droma).OnMobDeath(FeiyinMobDeath)
                .Npc(Zone.Feiyin, Npc.Feiyin.Drone).OnMobDeath(FeiyinMobDeath)
                .Npc(Zone.Feiyin, Npc.Feiyin.Jenglot).OnMobDeath(FeiyinMobDeath)
                .Npc(Zone.Feiyin, Npc.Feiyin.MindHoarder).OnMobDeath(FeiyinMobDeath)
                .Npc(Zone.Feiyin, Npc.Feiyin.OreGolem).OnMobDeath(FeiyinMobDeath)
                .Npc(Zone.Feiyin, Npc.Feiyin.Talos).OnMobDeath(FeiyinMobDeath)
                .OnEventFinish(Zone.Feiyin, 14, q =>
                {
                    q.Quest.SetStep(2);
                    q.Player.DelKeyItem(KeyItem.ShantottosNewSpell);
                })
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Shantotto).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(342);
                    }
                    else
                    {
                        return QuestReply.Event(341);
                    }
                })
                .OnEventFinish(Zone.WindurstWalls, 342, q =>
                {
                    // Per FFXIclopedia: You can still get [Total Loser] at the
                    // title-changing NPC after the quest has been completed.
                    q.Player.AddTitle(Title.TotalLoser);
                    q.Quest.Complete();
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Shantotto).OnTrigger(QuestReply.Event(343).ReplaceDefault());
        }
    }
}
