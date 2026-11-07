// Trust: Windurst (Log ID 2, Quest ID 96), ported from the trust branches of
// scripts/zones/Windurst_Woods/npcs/Wetata.lua and scripts/zones/Heavens_Tower/npcs/Kupipi.lua (quest rework B3b,
// Windurst group 32; call-only). Wetata : !pos -23.825 2.533 -44.567 241; Kupipi : !pos 2 0.1 30 242. Main's
// Trust: Bastok (a quest file) is the template.
// Wetata: main level >= 5, ENABLE_TRUST_QUESTS == 1 (the Lua value: 1.25 and true are not 1, so Settings.MainEquals),
// this quest available: Trust: Bastok and Trust: San d'Oria both available: 863; one of them completed: 867; else
// nothing at all (the arm taken, no event: NoAction, so her 868 is not played). Else the Green Institute Card held
// (any status): 864; completed: 861. Else her 868 (her script's). 863's / 867's finish, option 2: begun, the card
// (npcUtil.giveKeyItem). Her cipher trade (862 / 902) stays in her script.
// Kupipi: accepted and Trust: San d'Oria or Trust: Bastok completed: 439 (0, 0, 0, memories, 0, 0, 0, rank3); its
// finish: Kupipi learned (silent, YOU_LEARNED_TRUST), the card taken (KEYITEM_LOST), npcUtil.completeQuest with the
// Windurst Trust Permit. Accepted, WindurstFirstTrust 0: 435 (the same arguments); its finish: Kupipi learned, the
// var 1. WindurstFirstTrust 1 and the local var KupipiTrustChatFlag 0: 436, the flag set 1. WindurstFirstTrust 2: 437;
// its finish: the card taken, npcUtil.completeQuest with the permit, The Trustworthy and the var cleared, then
// CALL_MULTIPLE_ALTER_EGO. Completed without the Nanaa Mihgo trust and the flag 0: 438, the flag set 1. Else her
// nation arms (408 / 251, her script's). memories: The Three Kingdoms completed 2, Moon Reading completed 8;
// rank3: the char's rank in its own nation >= 3.
// The handlers that answer at Kupipi before her script (and so before this quest's call): a mission's progress event
// (Bastok 2-3's 239, ...), Waking the Colossus' 429 and Trust: Semih's noAction outrank it; a mission's event (the
// completed Windurst 2-3's 102, Bastok 2-3's 240) is a Default-priority action, so the lookup takes turns between it
// and her script. Wetata has no default and no other quest's handler.
// WindurstFirstTrust 1 -> 2 is set outside: the Kupipi trust spell (scripts/actions/spells/trust/kupipi.lua,
// onSpellCast) in East or West Sarutabaruta. It never names the quest: it stays in Lua (the flows set it by GM, as
// Trust: Bastok's Naji cast; PLATFORM-NEEDS `cast <spell>`).
// npcUtil.completeQuest's tables differ between 437 and 439, which one QuestReward cannot say: each finish makes the
// calls npcUtil.completeQuest makes, in its order (the key item by npcUtil.giveKeyItem; the title; the var; then the
// raw player:completeQuest, so the quest's Quest[2][96] vars stay), so no QuestReward is declared.
// The other nations' trust NPCs (Excenmille, Gondebaud, Clarion Star, Naji) and RoE record 508 read this quest's
// status: status reads that stay where they are.
// Kept as the Lua has it: Wetata's available arm with another trust quest accepted (neither available nor completed)
// plays nothing at all; 864 plays whenever the card is held, before the completed arm; 436 / 438 set the flag on the
// talk, so the chat plays once per zone; 437 does not read the flag; 439 completes without The Trustworthy,
// CALL_MULTIPLE_ALTER_EGO or clearing WindurstFirstTrust, which 437 gives; 438 checks only the Nanaa Mihgo trust (the
// other Windurst trusts are not read); a WindurstFirstTrust outside 0-2 (only a GM's) leaves Kupipi to her nation arms;
// no finish reads the status or the option (437 / 439 send KEYITEM_LOST for a card not held).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class TrustWindurst : QuestScript
    {
        const string FirstTrust = "WindurstFirstTrust";
        const string ChatFlag = "KupipiTrustChatFlag";

        public TrustWindurst() : base(QuestLog.Windurst, "TRUST_WINDURST") { }

        // Kupipi.lua trustMemory
        static int TrustMemory(IQuestPlayerView player)
        {
            int memories = 0;

            if (player.HasCompletedMission(MissionLog.Windurst, Mission.Windurst.TheThreeKingdoms))
            {
                memories += 2;
            }

            // 4 - nothing
            if (player.HasCompletedMission(MissionLog.Windurst, Mission.Windurst.MoonReading))
            {
                memories += 8;
            }

            // 16 - chocobo racing (commented out in the Lua)
            return memories;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Wetata).Called()
                    // Wetata.lua onTrigger, the trust arms (her 868 stays in her script)
                    .OnTrigger(q =>
                    {
                        QuestStatus trustSandoria = q.Player.QuestStatus(QuestLog.Sandoria, "TRUST_SANDORIA");
                        QuestStatus trustBastok = q.Player.QuestStatus(QuestLog.Bastok, "TRUST_BASTOK");
                        QuestStatus trustWindurst = q.Quest.Status;

                        if (q.Player.MainLevel >= 5 &&
                            q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
                            trustWindurst == QuestStatus.Available)
                        {
                            if (trustBastok == QuestStatus.Available && trustSandoria == QuestStatus.Available)
                            {
                                return QuestReply.Event(863);
                            }
                            else if (trustBastok == QuestStatus.Completed || trustSandoria == QuestStatus.Completed)
                            {
                                return QuestReply.Event(867);
                            }

                            return QuestReply.NoAction(); // the arm taken, no event: her 868 not played
                        }
                        else if (q.Player.HasKeyItem(KeyItem.GreenInstituteCard))
                        {
                            return QuestReply.Event(864);
                        }
                        else if (trustWindurst == QuestStatus.Completed)
                        {
                            return QuestReply.Event(861);
                        }

                        return null; // her 868
                    })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).Called()
                    // Kupipi.lua onTrigger, the trust arms (her nation arms stay in her script)
                    .OnTrigger(q =>
                    {
                        QuestStatus trustSandoria = q.Player.QuestStatus(QuestLog.Sandoria, "TRUST_SANDORIA");
                        QuestStatus trustBastok = q.Player.QuestStatus(QuestLog.Bastok, "TRUST_BASTOK");
                        QuestStatus trustWindurst = q.Quest.Status;
                        int windurstFirstTrust = q.Player.CharVar(FirstTrust);
                        int kupipiTrustChatFlag = q.Player.LocalVar(ChatFlag);
                        int rank3 = q.Player.Rank(q.Player.Nation) >= 3 ? 1 : 0;

                        if (trustWindurst == QuestStatus.Accepted &&
                            (trustSandoria == QuestStatus.Completed || trustBastok == QuestStatus.Completed))
                        {
                            return QuestReply.Event(439, 0, 0, 0, TrustMemory(q.Player), 0, 0, 0, rank3);
                        }
                        else if (trustWindurst == QuestStatus.Accepted && windurstFirstTrust == 0)
                        {
                            return QuestReply.Event(435, 0, 0, 0, TrustMemory(q.Player), 0, 0, 0, rank3);
                        }
                        else if (trustWindurst == QuestStatus.Accepted &&
                            windurstFirstTrust == 1 &&
                            kupipiTrustChatFlag == 0)
                        {
                            q.Player.SetLocalVar(ChatFlag, 1); // the Lua sets it right after startEvent(436)
                            return QuestReply.Event(436);
                        }
                        else if (trustWindurst == QuestStatus.Accepted && windurstFirstTrust == 2)
                        {
                            return QuestReply.Event(437);
                        }
                        else if (trustWindurst == QuestStatus.Completed &&
                            !q.Player.HasSpell(Spell.NanaaMihgo) &&
                            kupipiTrustChatFlag == 0)
                        {
                            q.Player.SetLocalVar(ChatFlag, 1); // the Lua sets it right after startEvent(438)
                            return QuestReply.Event(438);
                        }

                        return null; // her nation arms (408 / 251)
                    });

            b.AnyState()
                // Wetata.lua onEventFinish 863 / 867, option 2
                .OnEventFinish(Zone.WindurstWoods, 863, q => BeginOnOption2(q))
                .OnEventFinish(Zone.WindurstWoods, 867, q => BeginOnOption2(q))
                // Kupipi.lua onEventFinish 435 / 437 / 439
                .OnEventFinish(Zone.HeavensTower, 435, q =>
                {
                    q.Player.AddSpell(Spell.Kupipi, silentLog: true);
                    q.Text.Special(Text.HeavensTower.YOU_LEARNED_TRUST, 0, Spell.Kupipi);
                    q.Player.SetCharVar(FirstTrust, 1);
                })
                .OnEventFinish(Zone.HeavensTower, 437, q =>
                {
                    q.Player.DelKeyItem(KeyItem.GreenInstituteCard);
                    q.Text.Special(Text.HeavensTower.KEYITEM_LOST, KeyItem.GreenInstituteCard);
                    // npcUtil.completeQuest with the key item WINDURST_TRUST_PERMIT, the title THE_TRUSTWORTHY and
                    // the var WindurstFirstTrust, step for step: not quest:complete, whose cleanup would clear the
                    // quest's own Quest[2][96] vars
                    q.Player.GiveKeyItem(KeyItem.WindurstTrustPermit);
                    q.Player.AddTitle(Title.TheTrustworthy);
                    q.Player.SetCharVar(FirstTrust, 0);
                    q.Player.CompleteQuest(QuestLog.Windurst, "TRUST_WINDURST");
                    q.Text.Special(Text.HeavensTower.CALL_MULTIPLE_ALTER_EGO);
                })
                .OnEventFinish(Zone.HeavensTower, 439, q =>
                {
                    q.Player.AddSpell(Spell.Kupipi, silentLog: true);
                    q.Text.Special(Text.HeavensTower.YOU_LEARNED_TRUST, 0, Spell.Kupipi);
                    q.Player.DelKeyItem(KeyItem.GreenInstituteCard);
                    q.Text.Special(Text.HeavensTower.KEYITEM_LOST, KeyItem.GreenInstituteCard);
                    // npcUtil.completeQuest with the key item WINDURST_TRUST_PERMIT, step for step: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[2][96] vars
                    q.Player.GiveKeyItem(KeyItem.WindurstTrustPermit);
                    q.Player.CompleteQuest(QuestLog.Windurst, "TRUST_WINDURST");
                });
        }

        // Wetata.lua onEventFinish: (csid == 863 or csid == 867) and option == 2
        static void BeginOnOption2(IQuestContext q)
        {
            if (q.Option == 2)
            {
                q.Quest.Begin();
                q.Player.GiveKeyItem(KeyItem.GreenInstituteCard);
            }
        }
    }
}
