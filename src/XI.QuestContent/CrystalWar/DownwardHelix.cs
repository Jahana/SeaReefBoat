// Downward Helix (Log ID 7, Quest ID 33), a line-for-line port of scripts/quests/crystalWar/SCH_AF2_Downward_Helix.lua
// (quest rework B3b Crystal War, draft). The Eldieme Necropolis [S]: Erlene !pos 376.936 -39.999 17.914 (default 15).
// Sauromugue Champaign [S]: Indescript Markings !pos 322 24 113 (no default, no script).
// Available, On Sabbatical completed with its Timer (its 20 sets it to tomorrow) at or before today, a Scholar at or
// above AF2_QUEST_LEVEL: Erlene: 23, whose finish begins it. Accepted: the Southern San d'Oria [S] zone-in from East
// Ronfaure [S] at Prog 0: 65 (-> Prog 1). Erlene: Prog 1: 25 (-> 2); Prog 0: 24; Prog 2-3: 26; Prog 4+: 27, whose
// finish completes it (Scholar's Bracers) and sets this quest's Timer to tomorrow and its mustZone. The Sauromugue
// Champaign [S] zone-in from Rolanberry Fields [S] at Prog 2: 3 (-> 3). Indescript Markings: Prog 3: 4, whose finish
// gives Ulbrecht's Mortarboard and, given (npcUtil.giveKeyItem always answers true), sets Prog 4.
// Kept as the Lua has it: 23 begins on any option; On Sabbatical's mustZone is not read; 24 and 26 have no finish; the
// mortarboard is never taken; the Timer and the mustZone are written after the cleanup, by xi.quest.setVar /
// setMustZone on the quest's own id (no quest reads them); no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class DownwardHelix : QuestScript
    {
        public DownwardHelix() : base(QuestLog.CrystalWar, "DOWNWARD_HELIX") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/SCH_AF2_Downward_Helix";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4);

            b.Reward(new QuestReward
            {
                Item = Item.ScholarsBracers,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "ON_SABBATICAL") &&
                    // xi.quest.getVar(player, CRYSTAL_WAR, ON_SABBATICAL, 'Timer'): player:getVar('Quest[7][32]Timer')
                    q.Player.CharVar("Quest[7][32]Timer") <= q.Player.VanadielUniqueDay &&
                    q.Player.MainJob == Job.Sch &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL"))
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.Erlene).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(23);
                })
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 23, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .OnZoneIn(Zone.SouthernSanDoriaS, q =>
                {
                    if (q.Player.PreviousZone == Zone.EastRonfaureS &&
                        q.Quest.Step == 0)
                    {
                        return QuestReply.Event(65);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 65, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.Erlene).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;
                    if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(25);
                    }
                    else if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(24);
                    }
                    else if (questProgress < 4)
                    {
                        return QuestReply.ProgressEvent(26);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(27);
                    }
                })
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 25, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 27, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetQuestVar(
                            QuestLog.CrystalWar, "DOWNWARD_HELIX", "Timer", q.Player.VanadielUniqueDay + 1);
                        q.Player.SetQuestMustZone(QuestLog.CrystalWar, "DOWNWARD_HELIX");
                    }
                })
                .OnZoneIn(Zone.SauromugueChampaignS, q =>
                {
                    if (q.Player.PreviousZone == Zone.RolanberryFieldsS &&
                        q.Quest.Step == 2)
                    {
                        return QuestReply.Event(3);
                    }
                    return null;
                })
                .Npc(Zone.SauromugueChampaignS, Npc.SauromugueChampaignS.IndescriptMarkings).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(4);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SauromugueChampaignS, 3, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.SauromugueChampaignS, 4, q =>
                {
                    if (q.Player.GiveKeyItem(KeyItem.UlbrechtsMortarboard))
                    {
                        q.Quest.SetStep(4);
                    }
                });
        }
    }
}
