// The Call of the Sea (Log ID 4, Quest ID 67), a line-for-line port of scripts/quests/otherAreas/The_Call_of_the_Sea.lua
// (quest rework B3b). Tavnazian Safehold, no scripts: Equette !pos 1.260 -22.438 -18.161 (default 162), Leporaitceau
// !pos 74.981 -24.835 11.786 (default 180), Anteurephiaux !pos 81.063 -24.977 -2.481 (default 177); Misareaux Coast:
// qm_bloody_coffin !pos 641.281 0.295 -516.568 (default NOTHING_OUT_OF_ORDINARY) and the NM Bloody Coffin.
// Available (no other condition): Equette at Prog 0: 170 (Prog 1); Anteurephiaux at Prog 1: 172, whose finish begins the
// quest and sets Prog 2; Leporaitceau at Option 0: 171 (Option 1; the optional cutscene, before or during the quest).
// Accepted: Leporaitceau at Option 0: 171 (Option 1); Bloody Coffin's death: the char's local NMKilled 1 (any Prog); the
// ??? at Prog 2 with NMKilled 1: the Whispering Conch (npcUtil.giveKeyItem), Prog 3, noAction; at Prog 2 without and
// Bloody Coffin not up: Bloody Coffin popped (claimed, the ??? not hidden), FOUL_STENCH_OF_DEATH. Anteurephiaux at Prog 3:
// 173, whose finish takes the conch and sets Prog 4; Equette at Prog 4: 174, whose finish runs quest:complete (the Memento
// Muffler).
// Kept as the Lua has it: 172 begins the quest with Prog 2 (the available section's last step); NMKilled is a plain local
// var (Petals for Parelbriaux's Baumesel sets the same name); popFromQM's result is not read; 174's quest:complete is not
// checked (a full inventory: not completed, 174 again).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class TheCallOfTheSea : QuestScript
    {
        public TheCallOfTheSea() : base(QuestLog.OtherAreas, "THE_CALL_OF_THE_SEA") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/The_Call_of_the_Sea";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.MementoMuffler,
            });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Anteurephiaux).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(172);
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Equette).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(170);
                    }
                    return null;
                })
                // This is a missable optional cutscene that can play either before or during the quest.
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Leporaitceau).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(171);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 170, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 171, q =>
                {
                    q.Quest.SetVar("Option", 1);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 172, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(2);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Anteurephiaux).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(173);
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Equette).OnTrigger(q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.ProgressEvent(174);
                    }
                    return null;
                })
                // This is a missable optional cutscene that can play either before or during the quest.
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Leporaitceau).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(171);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 171, q =>
                {
                    q.Quest.SetVar("Option", 1);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 173, q =>
                {
                    q.Player.DelKeyItem(KeyItem.WhisperingConch);
                    q.Quest.SetStep(4);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 174, q =>
                {
                    q.Quest.Complete();
                })
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast.BloodyCoffin).OnMobDeath(q =>
                {
                    q.Player.SetLocalVar("NMKilled", 1);
                })
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast.qmbloodycoffin).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;
                    if (progress == 2 &&
                        q.Player.LocalVar("NMKilled") == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.WhisperingConch);
                        q.Quest.SetStep(3);
                        return QuestReply.NoAction();
                    }
                    else if (progress == 2 &&
                             !q.Player.MobIsSpawned(Mob.MisareauxCoast.BLOODY_COFFIN))
                    {
                        q.Player.PopFromQm(Mob.MisareauxCoast.BLOODY_COFFIN, hide: 0, claim: true);
                        return QuestReply.MessageSpecial(Text.MisareauxCoast.FOUL_STENCH_OF_DEATH);
                    }
                    return null;
                });
        }
    }
}
