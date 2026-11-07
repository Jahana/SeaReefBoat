// Trust: Bastok (Log ID 1, Quest ID 92), a line-for-line port of scripts/quests/bastok/Trust_Bastok.lua (quest rework
// B3b). Clarion Star : !pos 81.478 7.500 -24.169 236; Naji : !pos 64 -14 -4 237. From level 5 with ENABLE_TRUST_QUESTS,
// Clarion Star offers 434 (no other nation's trust quest begun) or 438 (one of them completed); option 2 begins it and
// gives the Blue Institute Card. Accepted: Clarion Star's 435 while the card is held; Naji's 984 when another nation's
// trust quest is completed (completed at once: Naji learned, the card taken), else 980 at Prog 0 (Naji learned, Prog
// 1), 981 (once per zone) at Prog 1 and 982 at Prog 2 (completed: The Trustworthy, the card taken). Completed: Ayame's
// 985, Lucius' 986 and Iron Eater's 988 (once per zone) teach their trusts on option 2, Naji's 983 (once per zone)
// until Ayame is learned, Clarion Star's 436.
// Prog 2 is set outside this file: the Naji trust spell (scripts/actions/spells/trust/naji.lua, onSpellCast) writes
// the char var Quest[1][92]Prog = 2 when it is cast in North or South Gustaberg at Prog 1. That var is this quest's
// Prog (Quest.Step, the same char var on C#), so the spell's read and write reach this quest unchanged.
// The trust NPCs of the other nations (Kupipi, Excenmille, Gondebaud, Wetata) and the RoE record 507 read this quest's
// status: status reads that stay as they are (no xi.cs call).
// Kept as the Lua file has it: trustMemoryIronEater is always 0 (every memory in it is commented out); 981 has no finish
// (Prog stays 1 until the trust is cast in Gustaberg); a Prog outside 0-2 (only a GM's) leaves Naji to his default;
// 984 completes the quest without The Trustworthy or CALL_MULTIPLE_ALTER_EGO, which 982 gives; Naji's 983 passes no
// memories (0); Ayame's 985 and Lucius' 986 are plain events (priority 100), not progress events, so another quest's
// handler at the same NPC can outrank them (Out of the Depths' Ayame reminder, priority 101); the `complete` of a
// finish is never false.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TrustBastok : QuestScript
    {
        public TrustBastok() : base(QuestLog.Bastok, "TRUST_BASTOK") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Trust_Bastok";

        // trustMemoryAyame
        static int TrustMemoryAyame(IQuestPlayerView player)
        {
            int memories = 0;

            // 2 - The Three Kingdoms
            if (player.HasCompletedMission(MissionLog.Sandoria, Mission.Sandoria.JourneyToBastok2) ||
                player.HasCompletedMission(MissionLog.Windurst, Mission.Windurst.TheThreeKingdomsBastok2))
            {
                memories += 2;
            }

            // 4 - Where Two Paths Converge
            if (player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.WhereTwoPathsConverge))
            {
                memories += 4;
            }

            // 8 - The Pirate's Cove
            if (player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.ThePiratesCove))
            {
                memories += 8;
            }

            // 16 - Ayame and Kaede
            if (player.HasCompletedQuest(QuestLog.Bastok, "AYAME_AND_KAEDE"))
            {
                memories += 16;
            }

            // 32 - Light of Judgement
            if (player.HasCompletedMission(MissionLog.Toau, Mission.Toau.LightOfJudgment))
            {
                memories += 32;
            }

            // 64 - True Strength
            if (player.HasCompletedQuest(QuestLog.Bastok, "TRUE_STRENGTH"))
            {
                memories += 64;
            }

            return memories;
        }

        // trustMemoryIronEater: every memory is a TODO in the Lua (commented out), so it is always 0
        static int TrustMemoryIronEater(IQuestPlayerView player)
        {
            int memories = 0;

            return memories;
        }

        // trustMemoryNaji
        static int TrustMemoryNaji(IQuestPlayerView player)
        {
            int memories = 0;

            if (player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.TheEmissary))
            {
                memories += 2;
            }

            if (player.HasCompletedQuest(QuestLog.Bastok, "THE_DOORMAN"))
            {
                memories += 4;
            }

            if (player.HasCompletedMission(MissionLog.Toau, Mission.Toau.LightOfJudgment))
            {
                memories += 8;
            }

            // 16 - Chocobo racing (commented out in the Lua)

            return memories;
        }

        // trustMemoryVolker
        static int TrustMemoryVolker(IQuestPlayerView player)
        {
            int memories = 0;

            // 2 - Darkness Rising (Bastok Mission)
            if (player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.DarknessRising))
            {
                memories += 2;
            }

            // 4 - Where Two Paths Converge (Bastok Mission)
            if (player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.WhereTwoPathsConverge))
            {
                memories += 4;
            }

            // 8 - Light of Judgment (Aht Urhgan Mission)
            if (player.HasCompletedMission(MissionLog.Toau, Mission.Toau.LightOfJudgment))
            {
                memories += 8;
            }

            // 16 - Hero's Combat (BCNM), commented out in the Lua

            return memories;
        }

        // player:getRank(player:getNation()) >= n and 1 or 0
        static int RankAtLeast(IQuestPlayerView player, int rank)
        {
            return player.Rank(player.Nation) >= rank ? 1 : 0;
        }

        // the finishes that teach a trust on option 2 (985, 986, 988)
        static void LearnOnOption2(IQuestContext q, int spell)
        {
            if (q.Option == 2)
            {
                q.Player.AddSpell(spell, silentLog: true);
                q.Text.Special(Text.Metalworks.YOU_LEARNED_TRUST, 0, spell);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { KeyItem = KeyItem.BastokTrustPermit });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= 5 &&
                    q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1))
                .Npc(Zone.PortBastok, Npc.PortBastok.ClarionStar).OnTrigger(q =>
                {
                    QuestStatus trustSandoria = q.Player.QuestStatus(QuestLog.Sandoria, "TRUST_SANDORIA");
                    QuestStatus trustWindurst = q.Player.QuestStatus(QuestLog.Windurst, "TRUST_WINDURST");

                    // no other trust tutorial quest begun
                    if (trustWindurst == QuestStatus.Available && trustSandoria == QuestStatus.Available)
                    {
                        return QuestReply.ProgressEvent(434);
                    }
                    // another trust quest finished
                    else if (trustWindurst == QuestStatus.Completed || trustSandoria == QuestStatus.Completed)
                    {
                        return QuestReply.ProgressEvent(438);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortBastok, 434, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.BlueInstituteCard);
                    }
                })
                .OnEventFinish(Zone.PortBastok, 438, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.BlueInstituteCard);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Metalworks, Npc.Metalworks.Naji).OnTrigger(q =>
                {
                    int rank3 = RankAtLeast(q.Player, 3);
                    int bastokFirstTrust = q.Quest.Step;

                    if (q.Player.QuestStatus(QuestLog.Sandoria, "TRUST_SANDORIA") == QuestStatus.Completed ||
                        q.Player.QuestStatus(QuestLog.Windurst, "TRUST_WINDURST") == QuestStatus.Completed)
                    {
                        return QuestReply.ProgressEvent(984, 0, 0, 0, TrustMemoryNaji(q.Player), 0, 0, 0, rank3);
                    }
                    else if (bastokFirstTrust == 0)
                    {
                        return QuestReply.ProgressEvent(980, 0, 0, 0, TrustMemoryNaji(q.Player), 0, 0, 0, rank3);
                    }
                    else if (bastokFirstTrust == 1)
                    {
                        return QuestReply.ProgressEvent(981).OncePerZone();
                    }
                    else if (bastokFirstTrust == 2)
                    {
                        return QuestReply.ProgressEvent(982);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 980, q =>
                {
                    q.Player.AddSpell(Spell.Naji, silentLog: true);
                    q.Text.Special(Text.Metalworks.YOU_LEARNED_TRUST, 0, Spell.Naji);
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.Metalworks, 982, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddTitle(Title.TheTrustworthy);
                        q.Player.DelKeyItem(KeyItem.BlueInstituteCard);
                        q.Text.Special(Text.Metalworks.KEYITEM_LOST, KeyItem.BlueInstituteCard);
                        q.Text.Special(Text.Metalworks.CALL_MULTIPLE_ALTER_EGO);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 984, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddSpell(Spell.Naji, silentLog: true);
                        q.Text.Special(Text.Metalworks.YOU_LEARNED_TRUST, 0, Spell.Naji);
                        q.Player.DelKeyItem(KeyItem.BlueInstituteCard);
                        q.Text.Special(Text.Metalworks.KEYITEM_LOST, KeyItem.BlueInstituteCard);
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.ClarionStar).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.BlueInstituteCard) ? QuestReply.ProgressEvent(435) : null);

            b.State(QuestStatus.Completed)
                .Npc(Zone.Metalworks, Npc.Metalworks.Ayame).OnTrigger(q =>
                {
                    int rank3 = RankAtLeast(q.Player, 3);

                    if (!q.Player.HasSpell(Spell.Ayame))
                    {
                        return QuestReply.Event(985, 0, 0, 0, TrustMemoryAyame(q.Player), 0, 0, 0, rank3);
                    }
                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.IronEater).OnTrigger(q =>
                {
                    if (q.Player.HasSpell(Spell.Naji) &&
                        q.Player.HasSpell(Spell.Ayame) &&
                        q.Player.HasSpell(Spell.Volker) &&
                        !q.Player.HasSpell(Spell.IronEater))
                    {
                        return QuestReply.ProgressEvent(988, 0, 0, 0, TrustMemoryIronEater(q.Player)).OncePerZone();
                    }
                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Lucius).OnTrigger(q =>
                {
                    int rank6 = RankAtLeast(q.Player, 6);

                    if (!q.Player.HasSpell(Spell.Volker))
                    {
                        return QuestReply.Event(986, 0, 0, 0, TrustMemoryVolker(q.Player), 0, 0, 0, rank6);
                    }
                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Naji).OnTrigger(q =>
                {
                    int rank3 = RankAtLeast(q.Player, 3);

                    if (!q.Player.HasSpell(Spell.Ayame))
                    {
                        return QuestReply.ProgressEvent(983, 0, 0, 0, 0, 0, 0, 0, rank3).OncePerZone();
                    }
                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 985, q => LearnOnOption2(q, Spell.Ayame))
                .OnEventFinish(Zone.Metalworks, 986, q => LearnOnOption2(q, Spell.Volker))
                .OnEventFinish(Zone.Metalworks, 988, q => LearnOnOption2(q, Spell.IronEater))
                .Npc(Zone.PortBastok, Npc.PortBastok.ClarionStar).OnTrigger(QuestReply.ProgressEvent(436));
        }
    }
}
