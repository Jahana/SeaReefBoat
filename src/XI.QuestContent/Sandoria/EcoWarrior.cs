// Eco-Warrior (San d'Oria) (Log ID 0, Quest ID 97), a line-for-line port of scripts/quests/sandoria/Eco_Warrior.lua (quest
// rework B3b, San d'Oria group 10). Norejaie : !pos 83.924 1 110.54 230; Rojaireaut : !pos -91.781 -0.545 587.944 193; qm7
// : !pos -90 30 156 193. Norejaie's 677 (San d'Oria fame level 1, EcoReset 0, no other nation's Eco-Warrior accepted;
// 682 when one is) begins it on option 1; Rojaireaut's 51 (option 1) puts on the ointment (Level Restriction 25,
// ON_ZONE); with it the qm7 at Prog 0 pops Necroplasm (claimed, looking at the char, the qm kept shown;
// OINTMENT_DRAWS_CREATURE); its death under the restriction sets Prog 1; the qm7 at Prog 1 gives the KI Indigested
// Stalagmite; Rojaireaut's 54 (option 0) sets Informed, 53 (option 0) takes the restriction off; Norejaie's 681
// completes it (Dragon Chronicles, 5000 gil, Vermillion Venturer, fame +20), deletes the stalagmite and sets EcoReset 1
// until the next conquest tally. Once completed and after the tally, Norejaie's 677 deletes the quest and begins it
// again.
// The Bastok and Windurst Eco-Warriors read this quest's status (src/XI.QuestContent/Bastok/EcoWarrior.cs,
// Windurst/EcoWarrior.cs: status reads). modules/era/lua/quests/sandoria/eco_warrior.lua
// (Modules/Era/Sandoria/EcoWarrior.cs) replaces state 2's finish of 51 with a copy whose
// restriction is 20. The San d'Oria twin of src/XI.QuestContent/Bastok/EcoWarrior.cs (one mob instead of two, no
// last-standing test).
// Kept as the Lua file has it: the stalagmite alone gates the turn-in (Rojaireaut's 54 is optional); 54 does not take
// the restriction off; Norejaie's 679 reminder plays at any Prog without the stalagmite; the fame level test (>= 1)
// always holds.
using XI.Quests;
using XI.Quests.Authoring;
using BastokEcoWarrior = XI.QuestContent.Bastok.EcoWarrior;

namespace XI.QuestContent.Sandoria
{
    public sealed class EcoWarrior : QuestScript
    {
        public EcoWarrior() : base(QuestLog.Sandoria, "ECO_WARRIOR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Eco_Warrior";

        // xi.effect.LEVEL_RESTRICTION and xi.effectFlag.ON_ZONE: Bastok's Eco-Warrior holds them (no generated class has
        // them)
        const int LevelRestriction = BastokEcoWarrior.LevelRestriction;
        const int EffectFlagOnZone = BastokEcoWarrior.EffectFlagOnZone;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Sandoria,
                Gil = 5000,
                Item = Item.DragonChronicles,
                Title = Title.VermillionVenturer,
            });

            // Section: Quest is available. Its status check matters for a completed char: with it dropped, section 1's
            // Norejaie 677 finish (Begin) would run as well as section 3's (DelQuest, Begin), and the extra Begin on a
            // completed quest changes the log packet (mutant e30, caught by sandoria.eco_warrior and #tally).
            b.State(QuestStatus.Available)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Norejaie).OnTrigger(NorejaieOffer)
                .OnEventFinish(Zone.SouthernSanDoria, 677, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.OrdellesCaves, Npc.OrdellesCaves.Necroplasm).OnMobDeath(q =>
                {
                    if (q.Player.HasStatusEffect(LevelRestriction))
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .Npc(Zone.OrdellesCaves, Npc.OrdellesCaves.qm7).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0 &&
                        q.Player.HasStatusEffect(LevelRestriction) &&
                        // npcUtil.popFromQM(player, npc, ordellesID.mob.NECROPLASM, { claim = true, look = true, hide = 0 }):
                        // popFromQM wraps a single id in a table, so the one-mob series is the same call
                        q.Player.PopFromQmSeries(Mob.OrdellesCaves.NECROPLASM, 1, hide: 0, claim: true, look: true))
                    {
                        return QuestReply.MessageSpecial(Text.OrdellesCaves.OINTMENT_DRAWS_CREATURE);
                    }
                    else if (q.Quest.Step == 1 && !q.Player.HasKeyItem(KeyItem.IndigestedStalagmite))
                    {
                        return QuestReply.KeyItem(KeyItem.IndigestedStalagmite);
                    }
                    return null;
                })
                .Npc(Zone.OrdellesCaves, Npc.OrdellesCaves.Rojaireaut).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.IndigestedStalagmite) && q.Quest.Var("Informed") == 0)
                    {
                        return QuestReply.ProgressEvent(54); // Sends the player to Norejaie.
                    }
                    else if (q.Player.HasStatusEffect(LevelRestriction))
                    {
                        return QuestReply.ProgressEvent(53); // Offers to remove the ointment.
                    }
                    else if (!q.Player.HasKeyItem(KeyItem.IndigestedStalagmite))
                    {
                        return QuestReply.ProgressEvent(51); // Offers to apply the ointment.
                    }
                    return null;
                })
                .OnEventFinish(Zone.OrdellesCaves, 51, q =>
                {
                    if (q.Option == 1)
                    {
                        // subPower 1: exp uses actual level and not the restricted level; the origin is the char
                        q.Player.AddStatusEffect(LevelRestriction, power: 25, subPower: 1, flag: EffectFlagOnZone);
                    }
                })
                .OnEventFinish(Zone.OrdellesCaves, 53, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.DelStatusEffect(LevelRestriction);
                    }
                })
                .OnEventFinish(Zone.OrdellesCaves, 54, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetVar("Informed", 1);
                    }
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Norejaie).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.IndigestedStalagmite))
                    {
                        return QuestReply.ProgressEvent(681); // Completes the quest.
                    }
                    else
                    {
                        return QuestReply.Event(679); // Reminder to see Rojaireaut.
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 681, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.IndigestedStalagmite);
                        q.Player.SetCharVar("EcoReset", 1, (uint)q.Player.NextConquestTally);
                    }
                });

            // Section: Quest completed.
            b.State(QuestStatus.Completed)
                // Repeatable after the next conquest tally.
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Norejaie).OnTrigger(NorejaieOffer)
                .OnEventFinish(Zone.SouthernSanDoria, 677, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelQuest(QuestLog.Sandoria, "ECO_WARRIOR");
                        q.Quest.Begin();
                    }
                });
        }

        // the available and the completed section's Norejaie onTrigger (the same code in both)
        static QuestReply NorejaieOffer(IQuestContext q)
        {
            if (q.Player.QuestStatus(QuestLog.Bastok, "ECO_WARRIOR") == QuestStatus.Accepted ||
                q.Player.QuestStatus(QuestLog.Windurst, "ECO_WARRIOR") == QuestStatus.Accepted)
            {
                return QuestReply.Event(682); // Already on another nation's Eco-Warrior.
            }
            else if (q.Player.FameLevel(FameArea.Sandoria) >= 1 && q.Player.CharVar("EcoReset") == 0)
            {
                return QuestReply.ProgressEvent(677); // Offers the quest.
            }
            return null;
        }
    }
}
