// Blue Ribbon Blues' era module, a line-for-line port of modules/era/lua/quests/windurst/blue_ribbon_blues.lua (quest
// rework B3b): it restores the JST midnight wait, and the zone change, before Kerutoto returns the purple ribbon (the July
// 8, 2014 version update replaced both with a flat wait, mistakenly one hour long; the November 10, 2014 update corrected
// the wait to one minute). Module:new('era_quest_blue_ribbon_blues', xi.pre(xi.expansion.SOA)); its onServerStart
// override wraps section 1's 358 and section 2's 365 (the quest's own finish, then Timer = JstMidnight() and the quest's
// mustZone) and replaces section 2's Windurst Waters Kerutoto onTrigger with a copy of the quest's handler whose wait
// branch also holds while the mustZone is set.
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Windurst
{
    public sealed class BlueRibbonBlues : QuestModule
    {
        public BlueRibbonBlues() : base("era_quest_blue_ribbon_blues", "modules/era/lua/quests/windurst/blue_ribbon_blues.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            m.Quest(QuestKey.Windurst.BlueRibbonBlues)
                // Kerutoto keeps the ribbon overnight, and only hands it back once the player has also zoned.
                // (quest.sections[1][xi.zone.WINDURST_WATERS].onEventFinish[358])
                .State(1).WrapOnEventFinish(Zone.WindurstWaters, 358, (q, baseFirstCS) =>
                {
                    baseFirstCS(q);
                    q.Quest.SetVar("Timer", q.Player.JstMidnight); // Module change
                    q.Quest.SetMustZone(); // Module change
                })
                // The player can hand the ribbon back a second time, which restarts the same wait.
                // (quest.sections[2][xi.zone.WINDURST_WATERS].onEventFinish[365])
                .State(2).WrapOnEventFinish(Zone.WindurstWaters, 365, (q, baseRetradeCS) =>
                {
                    baseRetradeCS(q);
                    q.Quest.SetVar("Timer", q.Player.JstMidnight); // Module change
                    q.Quest.SetMustZone(); // Module change
                })
                // Copy of the base handler, with the zone change added to the wait branch.
                // (quest.sections[2][xi.zone.WINDURST_WATERS]['Kerutoto'].onTrigger)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kerutoto).ReplaceOnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        if (q.Player.SystemTime < q.Quest.Var("Timer") ||
                            q.Quest.MustZone) // Module change
                        {
                            return QuestReply.ProgressEvent(359);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(360);
                        }
                    }
                    else if (questProgress == 1)
                    {
                        if (!q.Player.FindItem(Item.PurpleRibbon))
                        {
                            return QuestReply.ProgressEvent(366, 0, Item.PurpleRibbon);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(361, 0, Item.PurpleRibbon);
                        }
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.ProgressEvent(362);
                    }

                    return null;
                });
        }
    }
}
