// Behind the Smile (Log ID 4, Quest ID 77), a line-for-line port of scripts/quests/otherAreas/Behind_the_Smile.lua (quest
// rework B3b, Other Areas group 4). Tavnazian Safehold: Enaremand !pos 96.514 -41 51.613 (two NPCs of that name, 16883839
// and 16883840, the second cutscene_only; no script, default 537); Mhaura: Fyi Chalmwoh (no script, no default); Carpenters' Landing: qm_behind_the_smile (default
// NOTHING_OUT_OF_ORDINARY) and the NM Bullheaded Grosvez.
// Available once It's Raining Mannequins! is completed: Enaremand 533, whose finish begins the quest. Accepted: Enaremand,
// with the Red Oil: 534, else 541 (Event); 534's finish: quest:complete (the Mannequin Pumps), then the Red Oil taken. Fyi
// at Prog 0: 320, whose finish sets Prog 1; else nil. The ??? at Prog 1 with Bullheaded Grosvez not spawned: SpawnMob,
// updateClaim(player), and the reply STENCH_OF_DECAY (a messageSpecial); at Prog 2: Prog 3, npcUtil.giveKeyItem the Red Oil,
// noAction; else nil. Bullheaded Grosvez's death at Prog 1: Prog 2.
// Kept as the Lua has it: 320's finish sets Prog 1 at any Prog; the ??? hands out the oil before the noAction, with no event.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class BehindTheSmile : QuestScript
    {
        public BehindTheSmile() : base(QuestLog.OtherAreas, "BEHIND_THE_SMILE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Behind_the_Smile";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.MannequinPumps,
            });
            b.Steps(1, 2, 3);

            // START: Talk to Enaremand (J-7) on the upper level in Tavnazian Safehold
            // QUEST AVAILABLE
            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.OtherAreas, "ITS_RAINING_MANNEQUINS"))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Enaremand).OnTrigger(QuestReply.ProgressEvent(533))
                .OnEventFinish(Zone.TavnazianSafehold, 533, q =>
                {
                    q.Quest.Begin();
                });

            // QUEST ACCEPTED
            b.State(QuestStatus.Accepted)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Enaremand).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.RedOil))
                    {
                        return QuestReply.ProgressEvent(534);
                    }
                    else
                    {
                        return QuestReply.Event(541);
                    }
                })
                .OnEventFinish(Zone.TavnazianSafehold, 534, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.RedOil);
                    }
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.FyiChalmwoh).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(320);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Mhaura, 320, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.CarpentersLanding, Npc.CarpentersLanding.qmbehindthesmile).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;
                    if (questProgress == 1 &&
                        !q.Player.MobIsSpawned(Mob.CarpentersLanding.BULLHEADED_GROSVEZ))
                    {
                        // SpawnMob(carpentersLandingID.mob.BULLHEADED_GROSVEZ):updateClaim(player)
                        q.Player.SpawnMob(Mob.CarpentersLanding.BULLHEADED_GROSVEZ, 0, claim: true);
                        return QuestReply.MessageSpecial(Text.CarpentersLanding.STENCH_OF_DECAY);
                    }
                    else if (questProgress == 2)
                    {
                        q.Quest.SetStep(3);
                        q.Player.GiveKeyItem(KeyItem.RedOil);
                        return QuestReply.NoAction();
                    }
                    return null;
                })
                .Npc(Zone.CarpentersLanding, Npc.CarpentersLanding.BullheadedGrosvez).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        q.Quest.SetStep(2);
                    }
                });
        }
    }
}
