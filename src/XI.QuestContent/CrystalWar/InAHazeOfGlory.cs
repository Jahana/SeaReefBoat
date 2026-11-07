// In a Haze of Glory (Log ID 7, Quest ID 38), a line-for-line port of
// scripts/quests/crystalWar/WOTG_SAN_6_In_a_Haze_of_Glory.lua (quest rework B3b Crystal War, draft). Southern San
// d'Oria [S]: Rholont !pos -168 -2 56 (default 32). Garlaige Citadel [S]: Diordinne !pos -107.785 0 193.665 (default
// 14), Wooden Crates !pos -0.717 3.347 -99.535 (no default). Ghoyu's Reverie: the instance (not implemented).
// Available, Perils of the Griffon completed: Rholont 637 (an Event); Diordinne 31, whose finish gives the Number Eight
// Shelter Key and begins it. Accepted: Diordinne: the key held: Option 0: 37 (an Event, whose finish sets Option 1);
// else nil; no key: the Timer 0: 34, whose finish sets the Timer to tomorrow; at or before today: 36, whose finish
// clears the Timer and gives the key again; else 35 (an Event, oncePerZone). Wooden Crates: Prog 0: 32 (-> Prog 1).
// Ghoyu's Reverie's 10000 finish (the instance cleared; "needs to be verified upon implementation of the instance"):
// Prog 2, setPos into Garlaige Citadel [S]. The Garlaige Citadel [S] zone-in at Prog 2 (any zone): 33 (-> Prog 3).
// Rholont: Prog 3: from 18:00: 82, whose finish completes it (a Fullmetal Bullet) and sets The Price of Valor's Timer
// to tomorrow and its mustZone; before 18:00: 81 (an Event).
// Kept as the Lua has it: 31 begins on any option; nothing here takes the key (the instance would); Rholont answers nil
// while accepted below Prog 3; 82 completes with no check but the reward's fit; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class InAHazeOfGlory : QuestScript
    {
        public InAHazeOfGlory() : base(QuestLog.CrystalWar, "IN_A_HAZE_OF_GLORY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_SAN_6_In_a_Haze_of_Glory";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3);

            b.Reward(new QuestReward
            {
                Item = Item.FullmetalBullet,
            });

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.CrystalWar, "PERILS_OF_THE_GRIFFON"))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(QuestReply.Event(637))
                .Npc(Zone.GarlaigeCitadelS, Npc.GarlaigeCitadelS.Diordinne).OnTrigger(QuestReply.ProgressEvent(31))
                .OnEventFinish(Zone.GarlaigeCitadelS, 31, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.NumberEightShelterKey);
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.GarlaigeCitadelS, Npc.GarlaigeCitadelS.Diordinne).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.NumberEightShelterKey))
                    {
                        if (q.Quest.Var("Option") == 0)
                        {
                            return QuestReply.Event(37);
                        }
                    }
                    else
                    {
                        int questTimer = q.Quest.Var("Timer");

                        if (questTimer == 0)
                        {
                            return QuestReply.ProgressEvent(34);
                        }
                        else if (questTimer <= q.Player.VanadielUniqueDay)
                        {
                            return QuestReply.ProgressEvent(36);
                        }
                        else
                        {
                            return QuestReply.Event(35).OncePerZone();
                        }
                    }
                    return null;
                })
                .Npc(Zone.GarlaigeCitadelS, Npc.GarlaigeCitadelS.WoodenCrates).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(32);
                    }
                    return null;
                })
                .OnZoneIn(Zone.GarlaigeCitadelS, q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.Event(33);
                    }
                    return null;
                })
                .OnEventFinish(Zone.GarlaigeCitadelS, 32, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.GarlaigeCitadelS, 33, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.GarlaigeCitadelS, 34, q =>
                {
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                })
                .OnEventFinish(Zone.GarlaigeCitadelS, 36, q =>
                {
                    q.Quest.SetVar("Timer", 0);
                    q.Player.GiveKeyItem(KeyItem.NumberEightShelterKey);
                })
                .OnEventFinish(Zone.GarlaigeCitadelS, 37, q =>
                {
                    q.Quest.SetVar("Option", 1);
                })
                // TODO: The assumption for this mission script is to catch Event 1000 which is
                // sent once the battlefield has been cleared.  This needs to be verified upon
                // implementation of the instance.
                .OnEventFinish(Zone.GhoyusReverie, 10000, q =>
                {
                    q.Quest.SetStep(2);
                    q.Player.SetPos(2.436, 6.235, -99.966, 127, Zone.GarlaigeCitadelS);
                })
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        if (q.Player.VanadielHour >= 18)
                        {
                            return QuestReply.ProgressEvent(82);
                        }
                        else
                        {
                            // This Event is displayed the first time the condition is met, and
                            // then changes to 84.  They are the same message in both cases.

                            return QuestReply.Event(81);
                        }
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 82, q =>
                {
                    if (q.Quest.Complete())
                    {
                        // NOTE: The Price of Valor also requires specific mission progress; however, it is possible to hit the below
                        // conditions if this is a second or third nation being completed by the player.

                        q.Player.SetQuestVar(QuestLog.CrystalWar, "THE_PRICE_OF_VALOR", "Timer", q.Player.VanadielUniqueDay + 1);
                        q.Player.SetQuestMustZone(QuestLog.CrystalWar, "THE_PRICE_OF_VALOR");
                    }
                });
        }
    }
}
