// Secrets of Ovens Lost (Log ID 4, Quest ID 73), a line-for-line port of scripts/quests/otherAreas/Secrets_of_Ovens_Lost.lua
// (quest rework B3b; the file's header says "Spice Gals"). Tavnazian Safehold: Despachiaire !pos 111.209 -40.015
// -85.481 (no script, default 106), Jonette !pos -70.889 -11.212 9.954 (no script, default 365); the Sacrarium's and
// Phomiuna Aqueducts' qm_tavnazian_cookbook (scripts that only move them on a time trigger; defaults MUSTY_OLD_MANUSCRIPTS /
// NOTHING_OUT_HERE). Repeatable once per conquest tally.
// Section 1 (whatever the status): Wait < NextConquestTally(), Prog 0 and San d'Oria's Spice Gals completed. Despachiaire,
// while the quest is available and Option 0: 505 (Option 1). Jonette at Option 1: 506; else, the quest completed: 507
// (the repeat). 506 / 507 finished: begun (quest:begin) and Prog 1. Section 2 (whatever the status): Prog 1. Jonette
// with the Tavnazian Cookbook: 508, whose finish, when quest:complete gives Miratete's Memoirs, takes the cookbook and sets
// Wait = NextConquestTally(); the ???s without the cookbook: the key item (quest:keyItem).
// Kept as the Lua has it: neither check reads the status, so a GM-accepted quest at Prog 0 still answers section 1; the
// repeat's begin sets the current bit of a completed quest (its status stays completed: the completed bit wins), and
// quest:complete on a completed quest completes nothing new (completeQuest returns at once) but still gives the reward;
// Option stays 1 until quest:complete's cleanup clears it, so the repeat goes through Jonette's 507, not Despachiaire.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class SecretsOfOvensLost : QuestScript
    {
        public SecretsOfOvensLost() : base(QuestLog.OtherAreas, "SECRETS_OF_OVENS_LOST") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Secrets_of_Ovens_Lost";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.MiratetesMemoirs,
            });
            b.Steps(1);

            b.AnyState(when: q => q.Quest.Var("Wait") < q.Player.NextConquestTally &&
                                  q.Quest.Step == 0 &&
                                  q.Player.HasCompletedQuest(QuestLog.Sandoria, "SPICE_GALS"))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(q =>
                {
                    if (q.Player.QuestStatus(QuestLog.OtherAreas, "SECRETS_OF_OVENS_LOST") == QuestStatus.Available &&
                        q.Quest.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(505);
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Jonette).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 1)
                    {
                        return QuestReply.ProgressEvent(506);
                    }
                    else if (q.Player.QuestStatus(QuestLog.OtherAreas, "SECRETS_OF_OVENS_LOST") == QuestStatus.Completed)
                    {
                        return QuestReply.ProgressEvent(507); // Repeat after conquest tally
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 505, q =>
                {
                    q.Quest.SetVar("Option", 1);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 506, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 507, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(1);
                });

            b.AnyState(step: 1)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Jonette).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.TavnazianCookbook))
                    {
                        return QuestReply.ProgressEvent(508);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 508, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.TavnazianCookbook);
                        q.Quest.SetVar("Wait", q.Player.NextConquestTally);
                    }
                })
                .Npc(Zone.Sacrarium, Npc.Sacrarium.qmtavnaziancookbook).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.TavnazianCookbook))
                    {
                        return QuestReply.KeyItem(KeyItem.TavnazianCookbook);
                    }
                    return null;
                })
                .Npc(Zone.PhomiunaAqueducts, Npc.PhomiunaAqueducts.qmtavnaziancookbook).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.TavnazianCookbook))
                    {
                        return QuestReply.KeyItem(KeyItem.TavnazianCookbook);
                    }
                    return null;
                });
        }
    }
}
