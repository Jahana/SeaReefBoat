// Eco-Warrior (Windurst) (Log ID 2, Quest ID 84), a line-for-line port of scripts/quests/windurst/Eco_Warrior.lua (quest
// rework B3b, Windurst group 12). Lumomo : !pos -55.770 -5.499 18.914 238; Ahko Mhalijikhari : !pos -344.617 -12.226
// -166.233 198; qm2 : !pos 143 9 -219 198. Lumomo's 818 (Windurst fame level 1, EcoReset 0, no other nation's
// Eco-Warrior accepted; 823 when one is) begins it on option 1; Ahko Mhalijikhari's 62 (option 1) puts on the ointment
// (Level Restriction 25, ON_ZONE); with it the qm2 at Prog 0 pops the three Wyrmflies (claimed, looking at the char, the
// qm kept shown; OINTMENT_DRAWS_MONSTERS); the death of the last Wyrmfly standing, under the restriction, sets Prog 1;
// the qm2 at Prog 1 gives the KI Indigested Meat; Ahko's 65 (option 0) sets Informed, 64 (option 0) takes the
// restriction off; Lumomo's 822 completes it (Dragon Chronicles, 5000 gil, Emerald Exterminator, fame +20), deletes the
// meat and sets EcoReset 1 until the next conquest tally. Once completed and after the tally, Lumomo's 818 deletes the
// quest and begins it again.
// The Bastok and San d'Oria Eco-Warriors read this quest's status (src/XI.QuestContent/Bastok/EcoWarrior.cs,
// Sandoria/EcoWarrior.cs: status reads), and the three share the plain char var EcoReset.
// modules/era/lua/quests/windurst/eco_warrior.lua (Modules/Era/Windurst/EcoWarrior.cs) replaces state 2's finish of 62
// with a copy whose restriction is 20. The Windurst twin of src/XI.QuestContent/Bastok/EcoWarrior.cs (three mobs
// instead of two).
// Kept as the Lua file has it: the meat alone gates the turn-in (Ahko's 65 is optional); 65 does not take the
// restriction off; Lumomo's 820 reminder plays at any Prog without the meat; the fame level test (>= 1) always holds.
using XI.Quests;
using XI.Quests.Authoring;
using BastokEcoWarrior = XI.QuestContent.Bastok.EcoWarrior;

namespace XI.QuestContent.Windurst
{
    public sealed class EcoWarrior : QuestScript
    {
        public EcoWarrior() : base(QuestLog.Windurst, "ECO_WARRIOR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Eco_Warrior";

        // xi.effect.LEVEL_RESTRICTION and xi.effectFlag.ON_ZONE: Bastok's Eco-Warrior holds them (no generated class has
        // them)
        const int LevelRestriction = BastokEcoWarrior.LevelRestriction;
        const int EffectFlagOnZone = BastokEcoWarrior.EffectFlagOnZone;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Windurst,
                Gil = 5000,
                Item = Item.DragonChronicles,
                Title = Title.EmeraldExterminator,
            });

            // Section: Quest is available.
            b.State(QuestStatus.Available)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Lumomo).OnTrigger(LumomoOffer)
                .OnEventFinish(Zone.WindurstWaters, 818, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.MazeOfShakhrami, Npc.MazeOfShakhrami.AhkoMhalijikhari).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.IndigestedMeat) && q.Quest.Var("Informed") == 0)
                    {
                        return QuestReply.ProgressEvent(65); // Sends the player to Lumomo.
                    }
                    else if (q.Player.HasStatusEffect(LevelRestriction))
                    {
                        return QuestReply.ProgressEvent(64); // Offers to remove the ointment.
                    }
                    else if (!q.Player.HasKeyItem(KeyItem.IndigestedMeat))
                    {
                        return QuestReply.ProgressEvent(62); // Offers to apply the ointment.
                    }
                    return null;
                })
                .Npc(Zone.MazeOfShakhrami, Npc.MazeOfShakhrami.qm2).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0 &&
                        q.Player.HasStatusEffect(LevelRestriction) &&
                        // { WYRMFLY_OFFSET, WYRMFLY_OFFSET + 1, WYRMFLY_OFFSET + 2 }, { claim = true, look = true, hide = 0 }
                        q.Player.PopFromQmSeries(Mob.MazeOfShakhrami.WYRMFLY_OFFSET, 3, hide: 0, claim: true, look: true))
                    {
                        return QuestReply.MessageSpecial(Text.MazeOfShakhrami.OINTMENT_DRAWS_MONSTERS);
                    }
                    else if (q.Quest.Step == 1 && !q.Player.HasKeyItem(KeyItem.IndigestedMeat))
                    {
                        return QuestReply.KeyItem(KeyItem.IndigestedMeat);
                    }
                    return null;
                })
                .Npc(Zone.MazeOfShakhrami, Npc.MazeOfShakhrami.Wyrmfly).OnMobDeath(q =>
                {
                    if (!q.Player.HasStatusEffect(LevelRestriction))
                    {
                        return;
                    }

                    // for i = WYRMFLY_OFFSET, WYRMFLY_OFFSET + 2: GetMobByID(i) is never nil here (all three are Maze of
                    // Shakhrami mobs), so the Lua's nmCheck test always holds
                    for (int offset = 0; offset <= 2; offset++)
                    {
                        int i = q.Player.MobIdOf(Mob.MazeOfShakhrami.WYRMFLY_OFFSET) + offset;

                        if (i != q.Mob.Id && q.Player.MobIsAlive(Mob.MazeOfShakhrami.WYRMFLY_OFFSET, offset))
                        {
                            return;
                        }
                    }

                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.MazeOfShakhrami, 62, q =>
                {
                    if (q.Option == 1)
                    {
                        // subPower 1: exp uses actual level and not the restricted level; the origin is the char
                        q.Player.AddStatusEffect(LevelRestriction, power: 25, subPower: 1, flag: EffectFlagOnZone);
                    }
                })
                .OnEventFinish(Zone.MazeOfShakhrami, 64, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.DelStatusEffect(LevelRestriction);
                    }
                })
                .OnEventFinish(Zone.MazeOfShakhrami, 65, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetVar("Informed", 1);
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Lumomo).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.IndigestedMeat))
                    {
                        return QuestReply.ProgressEvent(822); // Completes the quest.
                    }
                    else
                    {
                        return QuestReply.Event(820); // Reminder to see Ahko Mhalijikhari.
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 822, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.IndigestedMeat);
                        q.Player.SetCharVar("EcoReset", 1, (uint)q.Player.NextConquestTally);
                    }
                });

            // Section: Quest completed.
            b.State(QuestStatus.Completed)
                // Repeatable after the next conquest tally.
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Lumomo).OnTrigger(LumomoOffer)
                .OnEventFinish(Zone.WindurstWaters, 818, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelQuest(QuestLog.Windurst, "ECO_WARRIOR");
                        q.Quest.Begin();
                    }
                });
        }

        // the available and the completed section's Lumomo onTrigger (the same code in both)
        static QuestReply LumomoOffer(IQuestContext q)
        {
            if (q.Player.QuestStatus(QuestLog.Sandoria, "ECO_WARRIOR") == QuestStatus.Accepted ||
                q.Player.QuestStatus(QuestLog.Bastok, "ECO_WARRIOR") == QuestStatus.Accepted)
            {
                return QuestReply.Event(823); // Already on another nation's Eco-Warrior.
            }
            else if (q.Player.FameLevel(FameArea.Windurst) >= 1 && q.Player.CharVar("EcoReset") == 0)
            {
                return QuestReply.ProgressEvent(818); // Offers the quest.
            }
            return null;
        }
    }
}
