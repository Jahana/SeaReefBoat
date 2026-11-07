// Dark Legacy (Log ID 1, Quest ID 57), a line-for-line port of scripts/quests/bastok/DRK_AF1_Dark_Legacy.lua (quest
// rework B3b). Raibaht : !pos -27 -10 -1 237; Mighty Fist : !pos -47 2 -30 237; Cochal-Monchal : !pos -52 -6 110 238;
// Quu Bokye : !pos -159 16 181 145; qm1 : !pos -58 0 -449 145. Raibaht's 751 (DRK, AF1 level), Mighty Fist's 752 (the
// Letter from the Darksteel Forge, Prog 1), Cochal-Monchal's 697 (the letter taken, Prog 2), the Yagudo Cherry at the
// qm1 (Vaa Huja the Erudite popped and claimed), his death (Prog 3), the Darksteel Formula from the qm1, Raibaht's 755.
// Kept as the Lua file has it: Quu Bokye's 62 takes a Yagudo Cherry and only sets Option 1 (it plays no part in the
// quest's progress); the qm1 hands out the formula again whenever it is not held at Prog 3.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class DarkLegacy : QuestScript
    {
        public DarkLegacy() : base(QuestLog.Bastok, "DARK_LEGACY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/DRK_AF1_Dark_Legacy";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Item = Item.RavenScythe });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainJob == Job.Drk &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL"))
                .Npc(Zone.Metalworks, Npc.Metalworks.Raibaht).OnTrigger(QuestReply.ProgressEvent(751))
                .OnEventFinish(Zone.Metalworks, 751, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Metalworks, Npc.Metalworks.MightyFist).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(752);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.DarksteelFormula))
                    {
                        return QuestReply.Event(754);
                    }
                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Raibaht).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.DarksteelFormula) ? QuestReply.ProgressEvent(755) : null)
                .OnEventFinish(Zone.Metalworks, 752, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.LetterFromTheDarksteelForge);
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.Metalworks, 755, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.DarksteelFormula);
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.CochalMonchal).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(697);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(698);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.DarksteelFormula))
                    {
                        return QuestReply.Event(699);
                    }
                    return null;
                })
                .OnEventFinish(Zone.WindurstWaters, 697, q =>
                {
                    q.Player.DelKeyItem(KeyItem.LetterFromTheDarksteelForge);
                    q.Quest.SetStep(2);
                })
                .Npc(Zone.Giddeus, Npc.Giddeus.qm1)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 2 &&
                            !q.Player.MobIsSpawned(Mob.Giddeus.VAA_HUJA_THE_ERUDITE, 0) &&
                            q.Trade.Matches((Item.YagudoCherry, 1)))
                        {
                            q.Trade.Complete();
                            q.Player.SpawnMob(Mob.Giddeus.VAA_HUJA_THE_ERUDITE, 0, claim: true);

                            return QuestReply.MessageSpecial(Text.Giddeus.SENSE_OF_FOREBODING);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                        !q.Player.HasKeyItem(KeyItem.DarksteelFormula) && q.Quest.Step == 3
                            ? QuestReply.KeyItem(KeyItem.DarksteelFormula)
                            : null)
                .Npc(Zone.Giddeus, Npc.Giddeus.QuuBokye)
                    .OnTrade(q =>
                        q.Quest.Step == 2 &&
                        q.Quest.Var("Option") == 0 &&
                        q.Trade.Matches((Item.YagudoCherry, 1))
                            ? QuestReply.ProgressEvent(62)
                            : null)
                    .OnTrigger(q => q.Quest.Step == 2 ? QuestReply.Event(61) : null)
                .Npc(Zone.Giddeus, Npc.Giddeus.VaaHujatheErudite).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        q.Quest.SetStep(3);
                    }
                })
                .OnEventFinish(Zone.Giddeus, 62, q =>
                {
                    q.Trade.Complete();

                    q.Quest.SetVar("Option", 1);
                });
        }
    }
}
