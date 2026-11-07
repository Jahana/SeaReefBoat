// I'll Take the Big Box (NIN AF2; Log ID 5, Quest ID 144), a line-for-line port of
// scripts/quests/outlands/Ill_Take_the_Big_Box.lua (quest rework B3b Outlands, group 3). Norg: Ryoma !pos -26 0.5 -8 (his
// script: Twenty in Pirate Years 133 / 134, True Will 136 / 137 / 138; default 94). Port Bastok: Ensetsu !pos 32 -6.8 68.8
// (his script: Twenty in Pirate Years 247 / 262 / 263; default 27). Rabao: Leodarion !pos -49.5 8 41.3 (his script: True
// Will 97 / 98 / 99; default 89). The ships bound for Selbina (and their pirates' copy): the NM Enagakure, which their
// Zone.lua spawns at night (20:00 to 04:00) for a char with the Seance Staff at Prog 4 (a var read: stays Lua). Selbina:
// the zone-in.
// Available with Twenty in Pirate Years completed: Ryoma 135 to a Ninja from AF2_QUEST_LEVEL whose needToZone flag is
// clear (else nil), which begins it. Accepted: Ensetsu 264 at Prog 0 (Prog 1), 265 at 1; Leodarion 90 at Prog 1 (Prog 2),
// 91 at 2; an Oak Pole alone (tradeMatches) at Prog 2: 92 (the trade taken by tradeComplete, Timer the next Vana'diel
// day, Prog 3); at 3 93 while VanadielUniqueDay() < Timer, else 94 (the Seance Staff, Prog 4); 95 at 4. Enagakure's death
// (either ship) at Prog 4 sets Prog 5; Selbina's zone-in at Prog 5 plays 1101 with the cutscene flags RESET_CAMERA |
// NO_PCS | NO_NPCS (0x13; textTable -1), whose finish completes it (the Ninja Hakama, Norg fame 20) and on that takes the
// staff.
// The zone-in returns the Lua table { 1101, -1, 0x13 } (luautils::OnZoneIn: id, textTable, flags) as
// Event(1101).TextTable(-1).Flags(0x13), the OnZoneIn table reply of quest rework P8; the #zonein flow plays it.
// Kept as the Lua has it: Ryoma's guard answers nil (his script then runs) rather than a section check; Leodarion's trade
// uses tradeMatches (the pole alone); 92 takes the trade by tradeComplete; Ensetsu and Leodarion are nil past their
// arms.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class IllTakeTheBigBox : QuestScript
    {
        // xi.cutsceneFlag.RESET_CAMERA | NO_PCS | NO_NPCS (scripts/enum/cutscene_flag.lua): the Lua zone-in's flags
        const uint ZoneInCutsceneFlags = 0x01 | 0x02 | 0x10;

        public IllTakeTheBigBox() : base(QuestLog.Outlands, "I_LL_TAKE_THE_BIG_BOX") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Ill_Take_the_Big_Box";

        // Enagakure's death in either ship
        static void EnagakureDeath(IQuestContext q)
        {
            if (q.Quest.Step == 4)
            {
                q.Quest.SetStep(5);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Norg,
                Item = Item.NinjaHakama,
            });
            b.Steps(1, 2, 3, 4, 5);

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Outlands, "TWENTY_IN_PIRATE_YEARS"))
                .Npc(Zone.Norg, Npc.Norg.Ryoma).OnTrigger(q =>
                {
                    if (q.Player.MainJob != Job.Nin ||
                        q.Player.MainLevel < q.Settings.MainNumber("AF2_QUEST_LEVEL") ||
                        q.Player.NeedToZone)
                    {
                        return null;
                    }

                    return QuestReply.ProgressEvent(135);
                })
                .OnEventFinish(Zone.Norg, 135, q =>
                {
                    q.Quest.Begin();
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Ensetsu).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.ProgressEvent(264);
                    }
                    else if (progress == 1)
                    {
                        return QuestReply.Event(265); // Reminder: Mitsunari's grandson lives in the Altepa Desert.
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortBastok, 264, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.Rabao, Npc.Rabao.Leodarion)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 2 &&
                            q.Trade.Matches((Item.OakPole, 1)))
                        {
                            return QuestReply.ProgressEvent(92);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int progress = q.Quest.Step;

                        if (progress == 1)
                        {
                            return QuestReply.ProgressEvent(90);
                        }
                        else if (progress == 2)
                        {
                            return QuestReply.Event(91); // Reminder: bring an oak pole.
                        }
                        else if (progress == 3)
                        {
                            if (q.Player.VanadielUniqueDay < q.Quest.Var("Timer"))
                            {
                                return QuestReply.Event(93); // Reminder: the trap is not finished yet.
                            }

                            return QuestReply.ProgressEvent(94);
                        }
                        else if (progress == 4)
                        {
                            return QuestReply.Event(95); // Reminder: sail the Bastore Sea from east to west at night.
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Rabao, 90, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.Rabao, 92, q =>
                {
                    q.Trade.Complete();
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.Rabao, 94, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.SeanceStaff);
                    q.Quest.SetStep(4);
                })
                .OnZoneIn(Zone.Selbina, q =>
                {
                    if (q.Quest.Step == 5)
                    {
                        // return { 1101, -1, bit.bor(xi.cutsceneFlag.RESET_CAMERA, xi.cutsceneFlag.NO_PCS, xi.cutsceneFlag.NO_NPCS) }
                        return QuestReply.Event(1101).TextTable(-1).Flags(ZoneInCutsceneFlags);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Selbina, 1101, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.SeanceStaff);
                    }
                })
                .Npc(Zone.ShipBoundForSelbina, Npc.ShipBoundForSelbina.Enagakure).OnMobDeath(EnagakureDeath)
                .Npc(Zone.ShipBoundForSelbinaPirates, Npc.ShipBoundForSelbinaPirates.Enagakure).OnMobDeath(EnagakureDeath);
        }
    }
}
