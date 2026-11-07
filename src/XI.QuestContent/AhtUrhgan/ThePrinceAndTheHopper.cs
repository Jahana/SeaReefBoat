// The Prince and the Hopper (Log ID 6, Quest ID 67), a line-for-line port of
// scripts/quests/ahtUrhgan/The_Prince_and_the_Hopper.lua (quest rework B3b Aht Urhgan, group 10). Aht Urhgan Whitegate:
// Maudaal !pos -147.2 1.8 -3.1 (no script; default 240). Mamook: Toads_Footprint2 !pos 216.1 -23.8 -102.5,
// Toads_Footprint1 !pos -42.8 6 -100.6 (no scripts, no defaults); the Poroggo Casanova (mob script: its despawn
// despawns the five after it) and its five Poroggo (MIKILULU, MIKIRURU, NIKILULU, MIKILURU, MIKIRULU). Wajaom
// Woodlands: its zone-in. Available (no other condition): Maudaal 889, finished with option 2: begun. Accepted:
// Wajaom's zone-in: Prog 0 with the char in the box 680 -19 218 .. 691 -14 221: 513 (Prog 1); Prog 2: 20, whose finish
// sets Prog 3 and the char down in Mamook (216.1 -23.818 -102.464). Mamook: Toads_Footprint2 at Prog 1: 222, whose
// finish sets Prog 2 and the char down in Wajaom (610.542 -28.547 356.247); Mamook's zone-in at Prog 3: 227 (Prog 4);
// Toads_Footprint1: Prog 4: 223 (Prog 5); Prog 5: the six mobs popped (the footprint hidden until they are all dead,
// then 1 s more) and, when they pop, IMPENDING_BATTLE sent by the handler (it returns nil); Prog 6: 225 (Prog 7). The
// Poroggo Casanova's death: Prog 5: Prog 6; the five after it despawned. Whitegate: Maudaal 890 at Prog 7, whose finish
// completes it (Chanoix's Gorget). Kept as the Lua has it: 889 begins on option 2 only; the Poroggo's death despawns
// the five at any Prog; the Footprint1 pop's message is the handler's own (no reply); no completed section; Maudaal
// returns nil below Prog 7 (240).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class ThePrinceAndTheHopper : QuestScript
    {
        public ThePrinceAndTheHopper() : base(QuestLog.AhtUrghan, "THE_PRINCE_AND_THE_HOPPER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/The_Prince_and_the_Hopper";

        // spawnedMobs, in the Lua's order
        static readonly string[] SpawnedMobs =
        {
            Mob.Mamook.MIKILULU,
            Mob.Mamook.MIKIRURU,
            Mob.Mamook.NIKILULU,
            Mob.Mamook.MIKILURU,
            Mob.Mamook.MIKIRULU,
            Mob.Mamook.POROGGO_CASANOVA,
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ChanoixsGorget,
            });
            b.Steps(1, 2, 3, 4, 5, 6, 7);

            // Section: Quest available
            b.State(QuestStatus.Available)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Maudaal).OnTrigger(QuestReply.ProgressEvent(889))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 889, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .OnZoneIn(Zone.WajaomWoodlands, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        double xPos = q.Player.XPos;
                        double yPos = q.Player.YPos;
                        double zPos = q.Player.ZPos;

                        if (xPos >= 680.0 && yPos >= -19.0 && zPos >= 218.0 &&
                            xPos <= 691.0 && yPos <= -14.0 && zPos <= 221.0)
                        {
                            return QuestReply.Event(513);
                        }
                    }
                    else if (q.Quest.Step == 2)
                    {
                        return QuestReply.Event(20);
                    }
                    return null;
                })
                .OnEventFinish(Zone.WajaomWoodlands, 20, q => // end of zone in cutscene
                {
                    q.Quest.SetStep(3);
                    // zone the person back to mamook
                    q.Player.SetPos(216.100, -23.818, -102.464, 0, Zone.Mamook);
                })
                .OnEventFinish(Zone.WajaomWoodlands, 513, q => // end of zone in cutscene
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.Mamook, Npc.Mamook.ToadsFootprint2).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(222);
                    }
                    return null;
                })
                .Npc(Zone.Mamook, Npc.Mamook.ToadsFootprint1).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 4)
                    {
                        return QuestReply.ProgressEvent(223);
                    }
                    else if (questProgress == 5)
                    {
                        // missing special spawn animation, cannot find in packet capture.
                        if (q.Player.PopFromQm(SpawnedMobs, hide: 1))
                        {
                            q.Text.Special(Text.Mamook.IMPENDING_BATTLE);
                        }
                    }
                    else if (questProgress == 6)
                    {
                        return QuestReply.ProgressEvent(225);
                    }
                    return null;
                })
                .Npc(Zone.Mamook, Npc.Mamook.PoroggoCasanova).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 5)
                    {
                        q.Quest.SetStep(6);
                    }

                    // for i = mamookID.mob.POROGGO_CASANOVA + 1, mamookID.mob.POROGGO_CASANOVA + 5 do DespawnMob(i) end
                    for (int i = 1; i <= 5; i++)
                    {
                        q.Player.DespawnMob(Mob.Mamook.POROGGO_CASANOVA, i);
                    }
                })
                .OnZoneIn(Zone.Mamook, q => q.Quest.Step == 3 ? QuestReply.Event(227) : null)
                .OnEventFinish(Zone.Mamook, 222, q =>
                {
                    q.Quest.SetStep(2);
                    // Zone the person to Wajaom for second part of cs
                    q.Player.SetPos(610.542, -28.547, 356.247, 0, Zone.WajaomWoodlands);
                })
                .OnEventFinish(Zone.Mamook, 223, q =>
                {
                    q.Quest.SetStep(5);
                })
                .OnEventFinish(Zone.Mamook, 225, q =>
                {
                    q.Quest.SetStep(7);
                })
                .OnEventFinish(Zone.Mamook, 227, q => // end of 2nd zone in cutscene
                {
                    q.Quest.SetStep(4);
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Maudaal).OnTrigger(q =>
                {
                    if (q.Quest.Step == 7)
                    {
                        return QuestReply.ProgressEvent(890);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 890, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
