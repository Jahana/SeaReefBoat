// For the Birds (Log ID 4, Quest ID 104), a line-for-line port of scripts/quests/otherAreas/For_The_Birds.lua (quest
// rework B3b, Other Areas group 8). Oldton Movalpolos: Koblakiq !pos -64.851 21.834 -117.521 (no script; the default
// event 13). Castle Oztroja: Daa Bola the Seer !pos -159.69 -16.26 191.923 (no script; default HERETICS). Beadeaux:
// GeFhu Yagudoeye !pos -91.354 -3.251 -127.831 (no script; default HUURR), the NMs Magnes Quadav (17379784, 17379786)
// and Nickel Quadav (17379785, 17379787).
// Available, Missionary Moblin completed and its mustZone clear: Koblakiq 14 (0, Arnica Root), whose finish begins the
// quest on option 1. Accepted: Koblakiq: Prog 0: 15 (event); 1: 16 (0, Glittering Fragment), whose finish sets Prog 2
// and gives the key item; 2: 17 (event); 4: 18, whose finish runs quest:complete (Jaguar Mantle) and on true sets the
// mustZone; 3 and above 4: nil. Daa Bola: a trade holding an Arnica Root: 87, whose finish confirms the trade, sets
// Prog 1 and the mustZone (whatever the Prog); his trigger: Prog 0: 86 (event); else the mustZone set: LETTING_YOU_GO;
// else nil. GeFhu: Prog 3 and Option 4: 124 (Prog 4 at its finish); Prog 2 and none of MAGNES, MAGNES + 1, NICKEL,
// NICKEL + 1 spawned: Prog 3, the four NMs MAGNES + 0..3 spawned and claimed, the Glittering Fragment taken
// (TAKEN_FROM_YOU), the reply QUADAV_ARE_ATTACKING; Prog 3 and none of the four spawned: the four spawned and claimed
// again, noAction; else nil. Each NM's death at Prog 3: Option + 1. Beadeaux's onZoneOut at Prog 2 and above: Option 0.
// Completed with the mustZone set: Koblakiq 19 (event).
// Kept as the Lua has it: the ids are interleaved (MAGNES 17379784, NICKEL 17379785), so MAGNES + 1 is NICKEL and the
// spawn checks read 17379784, 17379785 (twice) and 17379786: the second Nickel (17379787, MAGNES + 3) is never read, so
// with only it up the NMs count as gone and the trigger spawns the four again (SpawnMob of a mob that is up). Daa
// Bola's 87 plays at any Prog (an Arnica Root at Prog 2 sets Prog 1 back); the Prog 2 arm takes the key item and says
// so even when the char has none; the Option counts deaths at Prog 3 only; Prog 3 with an Option other than 4: the
// respawn arm (the four spawned again, noAction) when none of the read NMs is up, else nil.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class ForTheBirds : QuestScript
    {
        public ForTheBirds() : base(QuestLog.OtherAreas, "FOR_THE_BIRDS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/For_The_Birds";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.JaguarMantle,
            });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.OtherAreas, "MISSIONARY_MOBLIN") &&
                    !q.Player.QuestMustZone(QuestLog.OtherAreas, "MISSIONARY_MOBLIN"))
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Koblakiq).OnTrigger(q =>
                {
                    return QuestReply.ProgressCutscene(14, 0, Item.ArnicaRoot);
                })
                .OnEventFinish(Zone.OldtonMovalpolos, 14, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Koblakiq).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.Event(15); // Reminder
                    }
                    else if (progress == 1)
                    {
                        return QuestReply.ProgressEvent(16, 0, KeyItem.GlitteringFragment);
                    }
                    else if (progress == 2)
                    {
                        return QuestReply.Event(17); // Reminder
                    }
                    else if (progress == 4)
                    {
                        return QuestReply.ProgressCutscene(18);
                    }
                    return null;
                })
                .OnEventFinish(Zone.OldtonMovalpolos, 16, q =>
                {
                    q.Quest.SetStep(2);
                    q.Player.GiveKeyItem(KeyItem.GlitteringFragment);
                })
                .OnEventFinish(Zone.OldtonMovalpolos, 18, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Quest.SetMustZone();
                    }
                })
                .Npc(Zone.CastleOztroja, Npc.CastleOztroja.DaaBolatheSeer)
                .OnTrade(q =>
                {
                    if (q.Trade.Has(Item.ArnicaRoot))
                    {
                        return QuestReply.ProgressCutscene(87);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.Event(86);
                    }
                    else if (q.Quest.MustZone)
                    {
                        return QuestReply.MessageSpecial(Text.CastleOztroja.LETTING_YOU_GO);
                    }
                    return null;
                })
                .OnEventFinish(Zone.CastleOztroja, 87, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(1);
                    q.Quest.SetMustZone();
                })
                .Npc(Zone.Beadeaux, Npc.Beadeaux.GeFhuYagudoeye).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 3 &&
                        q.Quest.Var("Option") == 4)
                    {
                        return QuestReply.ProgressCutscene(124);
                    }
                    else if (progress == 2 &&
                        !q.Player.MobIsSpawned(Mob.Beadeaux.MAGNES_QUADAV_NM) &&
                        !q.Player.MobIsSpawned(Mob.Beadeaux.MAGNES_QUADAV_NM, 1) &&
                        !q.Player.MobIsSpawned(Mob.Beadeaux.NICKEL_QUADAV_NM) &&
                        !q.Player.MobIsSpawned(Mob.Beadeaux.NICKEL_QUADAV_NM, 1))
                    {
                        q.Quest.SetStep(3);
                        for (int i = 0; i <= 3; i++)
                        {
                            q.Player.SpawnMob(Mob.Beadeaux.MAGNES_QUADAV_NM, i, claim: true);
                        }

                        q.Player.DelKeyItem(KeyItem.GlitteringFragment);
                        q.Text.Special(Text.Beadeaux.TAKEN_FROM_YOU, 0, KeyItem.GlitteringFragment);
                        return QuestReply.MessageSpecial(Text.Beadeaux.QUADAV_ARE_ATTACKING);
                    }
                    else if (progress == 3 &&
                        !q.Player.MobIsSpawned(Mob.Beadeaux.MAGNES_QUADAV_NM) &&
                        !q.Player.MobIsSpawned(Mob.Beadeaux.MAGNES_QUADAV_NM, 1) &&
                        !q.Player.MobIsSpawned(Mob.Beadeaux.NICKEL_QUADAV_NM) &&
                        !q.Player.MobIsSpawned(Mob.Beadeaux.NICKEL_QUADAV_NM, 1))
                    {
                        for (int i = 0; i <= 3; i++)
                        {
                            q.Player.SpawnMob(Mob.Beadeaux.MAGNES_QUADAV_NM, i, claim: true);
                        }

                        return QuestReply.NoAction();
                    }
                    return null;
                })
                .Npc(Zone.Beadeaux, Npc.Beadeaux.MagnesQuadavNM).OnMobDeath(q =>
                {
                    int questOption = q.Quest.Var("Option");

                    if (q.Quest.Step == 3)
                    {
                        q.Quest.SetVar("Option", questOption + 1);
                    }
                })
                .Npc(Zone.Beadeaux, Npc.Beadeaux.NickelQuadavNM).OnMobDeath(q =>
                {
                    int questOption = q.Quest.Var("Option");

                    if (q.Quest.Step == 3)
                    {
                        q.Quest.SetVar("Option", questOption + 1);
                    }
                })
                .OnZoneOut(Zone.Beadeaux, q =>
                {
                    if (q.Quest.Step >= 2)
                    {
                        q.Quest.SetVar("Option", 0);
                    }
                })
                .OnEventFinish(Zone.Beadeaux, 124, q =>
                {
                    q.Quest.SetStep(4);
                });

            b.State(QuestStatus.Completed, when: q => q.Quest.MustZone)
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Koblakiq).OnTrigger(QuestReply.Event(19));
        }
    }
}
