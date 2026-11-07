// Trust: San d'Oria (Log ID 0, Quest ID 119), ported from the trust branches of
// scripts/zones/Southern_San_dOria/npcs/Gondebaud.lua and scripts/zones/Northern_San_dOria/npcs/Excenmille.lua
// (quest rework B3b, San d'Oria group 16; call-only). Gondebaud : !pos 123.754 0 92.125 230;
// Excenmille : !pos -229.344 6.999 22.976 231. Main's Trust: Bastok (TrustBastok.cs) and Trust: Windurst
// (TrustWindurst.cs) are the templates.
// Gondebaud: main level >= 5, ENABLE_TRUST_QUESTS 1, this quest available: Trust: Windurst and Trust: Bastok both
// available: 3500; one of them completed: 3504; else nothing at all (the arm taken, no event). Else the
// Red Institute Card held (any status): 3501; completed: 3502. Else his 3505 (his script's). 3500's / 3504's finish,
// option 2: begun, the card (npcUtil.giveKeyItem). His cipher trade (3503 / 3553) stays in his script.
// Excenmille: accepted and Trust: Windurst or Trust: Bastok completed: 897 (0, 0, 0, memories, 0, 0, 0, rank3) (its
// finish: Excenmille learned (silent, YOU_LEARNED_TRUST), the card taken (KEYITEM_LOST), npcUtil.completeQuest with the
// San d'Oria Trust Permit); accepted, SandoriaFirstTrust 0: 893 (the same arguments) (its finish: Excenmille learned,
// the var 1); 1 and the local var ExcenmilleTrustChatFlag 0: 894, the flag set 1; 2: 895 (its finish: the card taken,
// npcUtil.completeQuest with the permit, The Trustworthy and the var cleared, then CALL_MULTIPLE_ALTER_EGO). Completed
// without the Curilla trust and the flag 0: 896 (0, 0, 0, 0, 0, 0, 0, rank3: no memories), the flag set 1. Else
// his 29 (his script's). memories: the Ballista License held 2, Claws of the Griffon completed 8,
// Blood of Heroes completed 16 (4, chocobo racing, is commented out in the Lua); rank3: the char's rank in its own
// nation >= 3.
// SandoriaFirstTrust 1 -> 2 is set outside: the Excenmille trust spell (scripts/actions/spells/trust/excenmille.lua,
// onSpellCast) in West or East Ronfaure. It never names the quest: it stays in Lua (the flows set it by GM, as
// Trust: Bastok's Naji cast; PLATFORM-NEEDS `cast <spell>`).
// npcUtil.completeQuest's tables differ between 895 and 897, which one QuestReward cannot say: each finish makes the
// same calls npcUtil.completeQuest makes, in its order (the key item by npcUtil.giveKeyItem; the title; the var; then
// the completion), so no QuestReward is declared and the completion is the raw player:completeQuest (not
// quest:complete, whose cleanup would clear the quest's own Quest[0][119] vars).
// The other nations' trust NPCs (Kupipi, Wetata, Clarion Star, Naji) and RoE record (roe_records.lua) read this quest's
// status: status reads that stay where they are (main's TrustBastok.cs and TrustWindurst.cs).
// Kept as the Lua has it: Gondebaud's available arm with another trust quest accepted (neither available nor completed)
// plays nothing at all; 3501 plays whenever the card is held, before the completed arm; 894 / 896 set the flag on the
// talk, so the chat plays once per zone; 897 completes without The Trustworthy, CALL_MULTIPLE_ALTER_EGO or clearing
// SandoriaFirstTrust, which 895 gives; 896 checks only the Curilla trust and passes no memories; a SandoriaFirstTrust
// outside 0-2 (only a GM's) leaves Excenmille to his 29; no finish reads the status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TrustSandoria : QuestScript
    {
        const string FirstTrust = "SandoriaFirstTrust";
        const string ChatFlag = "ExcenmilleTrustChatFlag";

        public TrustSandoria() : base(QuestLog.Sandoria, "TRUST_SANDORIA") { }

        // Excenmille.lua trustMemory
        static int TrustMemory(IQuestPlayerView player)
        {
            int memories = 0;

            if (player.HasKeyItem(KeyItem.BallistaLicense))
            {
                memories += 2;
            }

            // 4 - chocobo racing (commented out in the Lua)
            if (player.HasCompletedQuest(QuestLog.CrystalWar, "CLAWS_OF_THE_GRIFFON"))
            {
                memories += 8;
            }

            if (player.HasCompletedQuest(QuestLog.CrystalWar, "BLOOD_OF_HEROES"))
            {
                memories += 16;
            }

            return memories;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Gondebaud).Called()
                    // Gondebaud.lua onTrigger, the trust arms (his 3505 stays in his script)
                    .OnTrigger(q =>
                    {
                        QuestStatus trustSandoria = q.Quest.Status;
                        QuestStatus trustBastok = q.Player.QuestStatus(QuestLog.Bastok, "TRUST_BASTOK");
                        QuestStatus trustWindurst = q.Player.QuestStatus(QuestLog.Windurst, "TRUST_WINDURST");

                        if (q.Player.MainLevel >= 5 &&
                            q.Settings.MainNumber("ENABLE_TRUST_QUESTS") == 1 &&
                            trustSandoria == QuestStatus.Available)
                        {
                            if (trustWindurst == QuestStatus.Available && trustBastok == QuestStatus.Available)
                            {
                                return QuestReply.Event(3500);
                            }
                            else if (trustWindurst == QuestStatus.Completed || trustBastok == QuestStatus.Completed)
                            {
                                return QuestReply.Event(3504);
                            }

                            return QuestReply.NoAction(); // the arm taken, no event: his 3505 not played
                        }
                        else if (q.Player.HasKeyItem(KeyItem.RedInstituteCard))
                        {
                            return QuestReply.Event(3501);
                        }
                        else if (trustSandoria == QuestStatus.Completed)
                        {
                            return QuestReply.Event(3502);
                        }

                        return null; // his 3505
                    })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Excenmille).Called()
                    // Excenmille.lua onTrigger, the trust arms (his 29 stays in his script)
                    .OnTrigger(q =>
                    {
                        QuestStatus trustSandoria = q.Quest.Status;
                        QuestStatus trustBastok = q.Player.QuestStatus(QuestLog.Bastok, "TRUST_BASTOK");
                        QuestStatus trustWindurst = q.Player.QuestStatus(QuestLog.Windurst, "TRUST_WINDURST");
                        int sandoriaFirstTrust = q.Player.CharVar(FirstTrust);
                        int excenmilleTrustChatFlag = q.Player.LocalVar(ChatFlag);
                        int rank3 = q.Player.Rank(q.Player.Nation) >= 3 ? 1 : 0;

                        if (trustSandoria == QuestStatus.Accepted &&
                            (trustWindurst == QuestStatus.Completed || trustBastok == QuestStatus.Completed))
                        {
                            return QuestReply.Event(897, 0, 0, 0, TrustMemory(q.Player), 0, 0, 0, rank3);
                        }
                        else if (trustSandoria == QuestStatus.Accepted && sandoriaFirstTrust == 0)
                        {
                            return QuestReply.Event(893, 0, 0, 0, TrustMemory(q.Player), 0, 0, 0, rank3);
                        }
                        else if (trustSandoria == QuestStatus.Accepted &&
                            sandoriaFirstTrust == 1 &&
                            excenmilleTrustChatFlag == 0)
                        {
                            q.Player.SetLocalVar(ChatFlag, 1); // the Lua sets it right after startEvent(894)
                            return QuestReply.Event(894);
                        }
                        else if (trustSandoria == QuestStatus.Accepted && sandoriaFirstTrust == 2)
                        {
                            return QuestReply.Event(895);
                        }
                        else if (trustSandoria == QuestStatus.Completed &&
                            !q.Player.HasSpell(Spell.Curilla) &&
                            excenmilleTrustChatFlag == 0)
                        {
                            q.Player.SetLocalVar(ChatFlag, 1); // the Lua sets it right after startEvent(896)
                            return QuestReply.Event(896, 0, 0, 0, 0, 0, 0, 0, rank3);
                        }

                        return null; // his 29
                    });

            b.AnyState()
                // Gondebaud.lua onEventFinish 3500 / 3504, option 2
                .OnEventFinish(Zone.SouthernSanDoria, 3500, q => BeginOnOption2(q))
                .OnEventFinish(Zone.SouthernSanDoria, 3504, q => BeginOnOption2(q))
                // Excenmille.lua onEventFinish 893 / 895 / 897
                .OnEventFinish(Zone.NorthernSanDoria, 893, q =>
                {
                    q.Player.AddSpell(Spell.Excenmille, silentLog: true);
                    q.Text.Special(Text.NorthernSanDoria.YOU_LEARNED_TRUST, 0, Spell.Excenmille);
                    q.Player.SetCharVar(FirstTrust, 1);
                })
                .OnEventFinish(Zone.NorthernSanDoria, 895, q =>
                {
                    q.Player.DelKeyItem(KeyItem.RedInstituteCard);
                    q.Text.Special(Text.NorthernSanDoria.KEYITEM_LOST, KeyItem.RedInstituteCard);
                    // npcUtil.completeQuest(player, SANDORIA, TRUST_SANDORIA, { keyItem = SAN_DORIA_TRUST_PERMIT,
                    // title = THE_TRUSTWORTHY, var = 'SandoriaFirstTrust' }), step for step
                    q.Player.GiveKeyItem(KeyItem.SanDoriaTrustPermit);
                    q.Player.AddTitle(Title.TheTrustworthy);
                    q.Player.SetCharVar(FirstTrust, 0);
                    q.Player.CompleteQuest(QuestLog.Sandoria, "TRUST_SANDORIA"); // npcUtil.completeQuest's
                    q.Text.Special(Text.NorthernSanDoria.CALL_MULTIPLE_ALTER_EGO);
                })
                .OnEventFinish(Zone.NorthernSanDoria, 897, q =>
                {
                    q.Player.AddSpell(Spell.Excenmille, silentLog: true);
                    q.Text.Special(Text.NorthernSanDoria.YOU_LEARNED_TRUST, 0, Spell.Excenmille);
                    q.Player.DelKeyItem(KeyItem.RedInstituteCard);
                    q.Text.Special(Text.NorthernSanDoria.KEYITEM_LOST, KeyItem.RedInstituteCard);
                    // npcUtil.completeQuest(player, SANDORIA, TRUST_SANDORIA, { keyItem = SAN_DORIA_TRUST_PERMIT }),
                    // step for step
                    q.Player.GiveKeyItem(KeyItem.SanDoriaTrustPermit);
                    q.Player.CompleteQuest(QuestLog.Sandoria, "TRUST_SANDORIA");
                });
        }

        static void BeginOnOption2(IQuestContext q)
        {
            if (q.Option == 2)
            {
                q.Quest.Begin();
                q.Player.GiveKeyItem(KeyItem.RedInstituteCard);
            }
        }
    }
}
