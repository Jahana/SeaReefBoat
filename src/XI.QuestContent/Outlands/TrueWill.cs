// True Will (Log ID 5), ported from the quest branches of scripts/zones/Norg/npcs/Ryoma.lua,
// scripts/zones/Rabao/npcs/Leodarion.lua, scripts/zones/Yhoator_Jungle/npcs/qm3.lua and the Kappa NMs
// (scripts/zones/Yhoator_Jungle/mobs/Kappa_Akuso.lua, Kappa_Biwa.lua, Kappa_Bonze.lua) (quest rework B3b, Outlands group
// 8; call-only: the scripts now call this quest; Ryoma's calls Twenty in Pirate Years first). Ryoma : !pos -23 0 -9 252;
// Leodarion : !pos -50 8 41 247; qm3 : !pos 203 0.1 82 124. Ryoma (94) and Leodarion (89) have a default that the lookup
// alternates with their script: every second talk runs the script; qm3 has none.
// Ryoma, after Twenty in Pirate Years' arms: I'll Take the Big Box completed and this quest available: 136 (its finish
// begins the quest, whatever the option); else the Old Trick Box and trueWillCS 0: 137 (its finish: trueWillCS 1); else
// trueWillCS 1: 138.
// Leodarion, accepted: trueWillCS 1: 97 (its finish: the Old Trick Box taken, trueWillCS 2); 2 and no Large Trick Box:
// 98; the Large Trick Box: 99 (its finish: npcUtil.completeQuest with the Ninja Chainmail, Norg fame +20,
// the Paragon of Ninja Excellence title and trueWillCS cleared, written out step for step, ending in the raw
// player:completeQuest, which keeps the quest's own Quest[5][145] vars; then the Large Trick Box taken).
// qm3, accepted and no Old Trick Box: trueWillKilledNM above 0: the Old Trick Box (npcUtil.giveKeyItem), the var 0; else
// CHILL_RUNS_DOWN and the three Kappa NMs popped (npcUtil.popFromQM, hide 0); else NOTHING_OUT_OF_ORDINARY.
// The Kappa NMs' death, accepted: the other two not alive: trueWillKilledNM + 1 (the last of the three to die counts).
// The rest stays in Lua (char var and key item only): Kuftal Tunnel's Treasure Coffer (trueWillCS 2: the Large Trick Box).
// Kept as the Lua has it: 137 and 138 read the key item / char var only (any status); qm3 shows CHILL_RUNS_DOWN while the
// NMs are up (popFromQM refuses); a death with no killer (player nil) raised in the Lua's status read: the patched mob
// scripts' call returns false on it (as Yagudo Avatar's).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TrueWill : QuestScript
    {
        public TrueWill() : base(QuestLog.Outlands, "TRUE_WILL") { }

        // the Kappa NMs: each one's death counts when the other two are not alive (the Lua's order of the two)
        private static readonly (string Kappa, string Other1, string Other2)[] Kappas =
        {
            (Npc.YhoatorJungle.KappaAkuso, Mob.YhoatorJungle.KAPPA_BONZE, Mob.YhoatorJungle.KAPPA_BIWA),
            (Npc.YhoatorJungle.KappaBiwa, Mob.YhoatorJungle.KAPPA_AKUSO, Mob.YhoatorJungle.KAPPA_BONZE),
            (Npc.YhoatorJungle.KappaBonze, Mob.YhoatorJungle.KAPPA_AKUSO, Mob.YhoatorJungle.KAPPA_BIWA),
        };

        protected override void Build(QuestScriptBuilder b)
        {
            // Ryoma.lua onTrigger: the arms after Twenty in Pirate Years', in his order
            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Outlands, "I_LL_TAKE_THE_BIG_BOX") == QuestStatus.Completed)
                .Npc(Zone.Norg, Npc.Norg.Ryoma).Called().OnTrigger(QuestReply.Event(136)); // Start Quest "True Will"

            b.AnyState(when: q => q.Player.HasKeyItem(KeyItem.OldTrickBox) && q.Player.CharVar("trueWillCS") == 0)
                .Npc(Zone.Norg, Npc.Norg.Ryoma).Called().OnTrigger(QuestReply.Event(137));

            b.AnyState(when: q => q.Player.CharVar("trueWillCS") == 1)
                .Npc(Zone.Norg, Npc.Norg.Ryoma).Called().OnTrigger(QuestReply.Event(138));

            var accepted = b.State(QuestStatus.Accepted);

            // Leodarion.lua onTrigger
            accepted
                .Npc(Zone.Rabao, Npc.Rabao.Leodarion).Called().OnTrigger(q =>
                {
                    int trueWillCS = q.Player.CharVar("trueWillCS");

                    if (trueWillCS == 1)
                    {
                        return QuestReply.Event(97);
                    }
                    else if (trueWillCS == 2 && !q.Player.HasKeyItem(KeyItem.LargeTrickBox))
                    {
                        return QuestReply.Event(98);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.LargeTrickBox))
                    {
                        return QuestReply.Event(99);
                    }

                    return null;
                });

            // the Kappa NMs' onMobDeath: only count the kill for the last alive/spawned NM dying
            foreach (var (kappa, other1, other2) in Kappas)
            {
                accepted
                    .Npc(Zone.YhoatorJungle, kappa).Called().OnMobDeath(q =>
                    {
                        if (!q.Player.MobIsAlive(other1) &&
                            !q.Player.MobIsAlive(other2))
                        {
                            // player:incrementCharVar('trueWillKilledNM', 1)
                            q.Player.SetCharVar("trueWillKilledNM", q.Player.CharVar("trueWillKilledNM") + 1);
                        }
                    });
            }

            // qm3.lua onTrigger
            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.OldTrickBox))
                .Npc(Zone.YhoatorJungle, Npc.YhoatorJungle.qm3).Called().OnTrigger(q =>
                {
                    if (q.Player.CharVar("trueWillKilledNM") > 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.OldTrickBox);
                        q.Player.SetCharVar("trueWillKilledNM", 0);
                    }
                    else
                    {
                        q.Text.Special(Text.YhoatorJungle.CHILL_RUNS_DOWN);
                        q.Player.PopFromQm(new[] { Mob.YhoatorJungle.KAPPA_AKUSO, Mob.YhoatorJungle.KAPPA_BONZE, Mob.YhoatorJungle.KAPPA_BIWA }, 0);
                    }

                    return QuestReply.NoAction();
                });

            b.AnyState()
                .Npc(Zone.YhoatorJungle, Npc.YhoatorJungle.qm3).Called()
                    .OnTrigger(QuestReply.MessageSpecial(Text.YhoatorJungle.NOTHING_OUT_OF_ORDINARY));

            // Ryoma.lua onEventFinish (136, 137), Leodarion.lua onEventFinish (97, 99)
            b.AnyState()
                .OnEventFinish(Zone.Norg, 136, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.Norg, 137, q =>
                {
                    q.Player.SetCharVar("trueWillCS", 1);
                })
                .OnEventFinish(Zone.Rabao, 97, q =>
                {
                    q.Player.DelKeyItem(KeyItem.OldTrickBox);
                    q.Player.SetCharVar("trueWillCS", 2);
                })
                .OnEventFinish(Zone.Rabao, 99, q =>
                {
                    // npcUtil.completeQuest step for step, in its order, ending in the raw player:completeQuest: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[5][145] vars
                    if (q.Player.GiveItem(Item.NinjaChainmail)) // Ninja Chainmail
                    {
                        q.Player.AddFame(FameArea.Norg, 20);
                        q.Player.AddTitle(Title.ParagonOfNinjaExcellence);
                        q.Player.SetCharVar("trueWillCS", 0);
                        q.Player.CompleteQuest(QuestLog.Outlands, "TRUE_WILL");
                        q.Player.DelKeyItem(KeyItem.LargeTrickBox);
                    }
                });
        }
    }
}
