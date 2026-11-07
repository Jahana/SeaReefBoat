// Blood of Heroes (Log ID 7, Quest ID 52), a line-for-line port of
// scripts/quests/crystalWar/WOTG_SAN_10_Blood_of_Heroes.lua (quest rework B3b Crystal War, draft). Southern San d'Oria
// [S]: Rholont !pos -168 -2 56 (default 32). Xarcabard [S]: Animal Spoor !pos 543.954 -0.522 -290.313, Wheel Rut !pos
// 345.162 -0.743 -309.161 (defaults NOTHING_OUT_OF_ORDINARY), Forbidding Portal !pos 320 -10.835 158.699 (default
// NO_RESPONSE). Ghoyu's Reverie: the instance (not implemented). Available, Songbirds in a Snowstorm completed:
// Rholont: 656 (an Event). Animal Spoor: 1, whose finish begins it. Accepted: Wheel Rut: Prog 0: 2 (-> Prog 1); Prog 5:
// 7, whose finish completes it (a Ram Staff, House Aurchiat Retainer). Animal Spoor: Prog 1: 3, whose finish gives the
// Vial of Military Prism Powder and sets Prog 2; Prog 3 without the vial: the Timer at or before today: 13, whose
// finish gives it again; else 15 (an Event). Forbidding Portal: Prog 2: 4 (-> 3). Ghoyu's Reverie's 10000 finish: Prog
// 4, setPos into Xarcabard [S]. The Xarcabard [S] zone-in at Prog 4 (any zone): 6 (-> 5). 7's finish, once
// completed, sets Chasing Shadows' Timer to tomorrow.
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): the Lua's 7 finish calls
// xi.quest.setVar(player, CRYSTAL_WAR, CHASING_SHADOWS, VanadielUniqueDay() + 1) with no var name: the day is taken as
// the name and the value is nil, so player:setVar raises (the binding wants a number) and writes nothing, after the
// completion; the C# sets the Timer that Chasing Shadows' check reads, as the call means to.
// Kept as the Lua has it: 1 begins on any option; nothing sets this quest's Timer (the Lua's NOTE: the instance's
// failure would), so 13 always answers; the vial is never taken; Prog 3 waits on the instance; the Xarcabard zone-in at
// Prog 4 plays from any zone; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class BloodOfHeroes : QuestScript
    {
        public BloodOfHeroes() : base(QuestLog.CrystalWar, "BLOOD_OF_HEROES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_SAN_10_Blood_of_Heroes";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5);

            b.Reward(new QuestReward
            {
                Item = Item.RamStaff,
                Title = Title.HouseAurchiatRetainer,
            });

            b.State(QuestStatus.Available,
                    when: q => q.Player.HasCompletedQuest(QuestLog.CrystalWar, "SONGBIRDS_IN_A_SNOWSTORM"))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(QuestReply.Event(656))
                .Npc(Zone.XarcabardS, Npc.XarcabardS.AnimalSpoor).OnTrigger(QuestReply.ProgressEvent(1))
                .OnEventFinish(Zone.XarcabardS, 1, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.XarcabardS, Npc.XarcabardS.AnimalSpoor).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(3);
                    }
                    else if (questProgress == 3 &&
                        !q.Player.HasKeyItem(KeyItem.VialOfMilitaryPrismPowder))
                    {
                        // the Lua's NOTE: the instance's failure is to set the Timer to tomorrow (not implemented)
                        if (q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                        {
                            return QuestReply.ProgressEvent(13);
                        }
                        else
                        {
                            return QuestReply.Event(15);
                        }
                    }
                    return null;
                })
                .Npc(Zone.XarcabardS, Npc.XarcabardS.ForbiddingPortal).OnTrigger(q =>
                {
                    // the Lua's TODO: the instance's entry (Prog 3 and the vial: Event 12, params 0, 0, 52) is not here
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(4);
                    }
                    return null;
                })
                .Npc(Zone.XarcabardS, Npc.XarcabardS.WheelRut).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(2);
                    }
                    else if (questProgress == 5)
                    {
                        return QuestReply.ProgressEvent(7);
                    }
                    return null;
                })
                .OnZoneIn(Zone.XarcabardS, q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.Event(6);
                    }
                    return null;
                })
                .OnEventFinish(Zone.XarcabardS, 2, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.XarcabardS, 3, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.VialOfMilitaryPrismPowder);
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.XarcabardS, 4, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.XarcabardS, 6, q =>
                {
                    q.Quest.SetStep(5);
                })
                .OnEventFinish(Zone.XarcabardS, 7, q =>
                {
                    if (q.Quest.Complete())
                    {
                        // the Lua's setVar named no var and raised (fixed, user ruling 2026-11-04)
                        q.Player.SetQuestVar(QuestLog.CrystalWar, "CHASING_SHADOWS", "Timer",
                            q.Player.VanadielUniqueDay + 1);
                    }
                })
                .OnEventFinish(Zone.XarcabardS, 13, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.VialOfMilitaryPrismPowder);
                })
                .OnEventFinish(Zone.GhoyusReverie, 10000, q =>
                {
                    // the Lua's TODO: the battlefield's clear, to be verified once the instance exists
                    q.Quest.SetStep(4);
                    q.Player.SetPos(319.8, -7.887, 153.741, 65, Zone.XarcabardS);
                });
        }
    }
}
