// Sharpening the Sword (Log ID 0, Quest ID 90, PLD AF1), a line-for-line port of scripts/quests/sandoria/Sharpening_the_Sword.lua
// (quest rework B3b, San d'Oria group 3). Ailbeche : !pos 4 -1 24 231; Sobane : !pos -190 -3 97 230; Stalagmite : !pos -51
// 0.1 3 193. Offered to a PLD of AF1_QUEST_LEVEL with the Family Counselor title (Father and Son): Ailbeche's 45 (option
// 1 begins it, any other option sets the quest var Option 1), then 43 (priority 200; option 1 clears Option and begins
// it). Accepted without the Ordelle Whetstone: Ailbeche's 42, Sobane's 52; in Ordelle's Caves the Stalagmite pops Polevik
// (noAction), or with Polevik up says DRY_WIND + 1 (the whetstone) and DRY_WIND; Polevik's death sets the quest local
// var Stage, and the next Stalagmite talk gives the whetstone. With the whetstone: Sobane's 52, Ailbeche's 44: completed
// (Honor Sword, fame +20), the whetstone deleted.
//
// Deliberate difference from upstream Lua (user ruling 2026-11-05, kept bugs): check 4 (completed) reads
// `player:getQuestStatus(xi.questLog.SANDORIA, xi.quest.id.sandoria.A_BOY_S_DREAM) == xi.quest.status.AVAILABLE`. The id
// is A_BOYS_DREAM (no A_BOY_S_DREAM exists) and xi.quest.status does not exist, so the Lua check raises an error ("bad
// argument #3") on any talk to Ailbeche once this quest is completed; the lookup runs each check under pcall
// (interaction_lookup.lua), so the error is printed and the section skipped. The port reads A_BOYS_DREAM against
// QuestStatus.Available, as Father and Son's completed section reads this quest: once completed, while A Boy's Dream
// is available, Ailbeche answers with AILBECHE_WHEN_FISHING (replaceDefault, over his default 868; the lookup
// alternates it with his script, which plays A Boy's Dream's offer to a PLD 50). The flow
// sandoria.sharpening_the_sword#completed shows it.
// Ailbeche's own script (A Boy's Dream, Under Oath) reads this quest's status; RoE records and Vingijard read it (stay).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class SharpeningTheSword : QuestScript
    {
        public SharpeningTheSword() : base(QuestLog.Sandoria, "SHARPENING_THE_SWORD") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Sharpening_the_Sword";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Item = Item.HonorSword });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasTitle(Title.FamilyCounselor) &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL") &&
                    q.Player.MainJob == Job.Pld)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(45);
                    }
                    else
                    {
                        return QuestReply.Event(43).SetPriority(200);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 43, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetVar("Option", 0);
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 45, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Quest.SetVar("Option", 1);
                    }
                });

            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.OrdelleWhetstone))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche).OnTrigger(QuestReply.Event(42))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sobane).OnTrigger(QuestReply.Event(52))
                .Npc(Zone.OrdellesCaves, Npc.OrdellesCaves.Stalagmite).OnTrigger(q =>
                {
                    int hasKilled = q.Quest.LocalVar("Stage");
                    if (hasKilled == 1)
                    {
                        q.Quest.SetLocalVar("Stage", 0);
                        return QuestReply.KeyItem(KeyItem.OrdelleWhetstone);
                    }
                    // { hide = 0 } with no claim: popFromQM's claim defaults to true
                    else if (q.Player.PopFromQm(Mob.OrdellesCaves.POLEVIK, claim: true, hide: 0))
                    {
                        return QuestReply.NoAction();
                    }
                    else
                    {
                        // player:messageSpecial(cavesID.text.DRY_WIND + 1, xi.keyItem.ORDELLE_WHETSTONE)
                        q.Text.Special(q.Text.Id(Text.OrdellesCaves.DRY_WIND) + 1, KeyItem.OrdelleWhetstone);
                        return QuestReply.MessageSpecial(Text.OrdellesCaves.DRY_WIND);
                    }
                })
                .Npc(Zone.OrdellesCaves, Npc.OrdellesCaves.Polevik).OnMobDeath(q => q.Quest.SetLocalVar("Stage", 1));

            b.State(QuestStatus.Accepted, when: q => q.Player.HasKeyItem(KeyItem.OrdelleWhetstone))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sobane).OnTrigger(QuestReply.Event(52))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche).OnTrigger(QuestReply.ProgressEvent(44))
                .OnEventFinish(Zone.NorthernSanDoria, 44, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.OrdelleWhetstone);
                    }
                });

            // a deliberate difference from upstream Lua (user ruling 2026-11-05, kept bugs; see the header): the Lua
            // reads the misspelt A_BOY_S_DREAM and xi.quest.status (both nil) and raises; this reads A Boy's Dream's
            // status
            b.State(QuestStatus.Completed, when: q =>
                    q.Player.QuestStatus(QuestLog.Sandoria, "A_BOYS_DREAM") == QuestStatus.Available)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche)
                    .OnTrigger(QuestReply.MessageText(Text.NorthernSanDoria.AILBECHE_WHEN_FISHING).ReplaceDefault());
        }
    }
}
