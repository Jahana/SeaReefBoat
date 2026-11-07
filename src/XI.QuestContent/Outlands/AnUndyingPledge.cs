// An Undying Pledge (Log ID 5), ported from the quest branches of scripts/zones/Norg/npcs/Stray_Cloud.lua (quest rework
// B3b, Outlands group 6; call-only: the script now calls this quest). Stray Cloud : !pos -19.238 1.097 -22.977 252 (the
// first of his two entities; the second, at the script's !pos -20.617 1.097 -29.165, loads hidden). His default
// (DefaultActions 231) alternates with his script in the lookup: every second talk runs the script, so this quest.
// Stray Cloud, in the script's order: available and Norg fame level 4: 225 (its finish begins the quest and sets the char
// var anUndyingPledgeCS 1); accepted with the Caliginous Blade: 227 (its finish: npcUtil.completeQuest with
// the Light Buckler, Norg fame +40 and the var cleared, step for step, ending in the raw player:completeQuest, which
// keeps the quest's own Quest[5][149] vars; then the blade taken); accepted, anUndyingPledgeCS 1: 228; 2: 229;
// completed: 230. The script keeps its 231 (the standard talk) when the quest does not act.
// The rest of the quest is in other scripts and stays Lua (char var only): Norg's Zone.lua (the trigger area's 226 sets
// anUndyingPledgeCS 2), Sea Serpent Grotto's Glyryvilu and qm5 (the blade, anUndyingPledgeCS 3).
// Kept as the Lua has it: the finishes read no status (225 begins from any status it is played in; 227 completes);
// accepted with anUndyingPledgeCS 3 and no blade answers nothing (the script's 231); the blade is taken only when the
// completion gave the buckler.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class AnUndyingPledge : QuestScript
    {
        public AnUndyingPledge() : base(QuestLog.Outlands, "AN_UNDYING_PLEDGE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Stray_Cloud.lua onTrigger, in its order
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Norg) >= 4)
                .Npc(Zone.Norg, Npc.Norg.StrayCloud).Called().OnTrigger(QuestReply.Event(225)); // Start quest

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Norg, Npc.Norg.StrayCloud).Called().OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.CaliginousBlade))
                    {
                        return QuestReply.Event(227); // Quest Finish
                    }
                    else if (q.Player.CharVar("anUndyingPledgeCS") == 1)
                    {
                        return QuestReply.Event(228); // Extra Dialogue
                    }
                    else if (q.Player.CharVar("anUndyingPledgeCS") == 2)
                    {
                        return QuestReply.Event(229); // Extra Dialogue
                    }

                    return null;
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Norg, Npc.Norg.StrayCloud).Called().OnTrigger(QuestReply.Event(230));

            // Stray_Cloud.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.Norg, 225, q =>
                {
                    q.Quest.Begin();
                    q.Player.SetCharVar("anUndyingPledgeCS", 1);
                })
                .OnEventFinish(Zone.Norg, 227, q =>
                {
                    // npcUtil.completeQuest step for step, in its order, ending in the raw player:completeQuest: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[5][149] vars
                    if (q.Player.GiveItem(Item.LightBuckler))
                    {
                        q.Player.AddFame(FameArea.Norg, 40);
                        q.Player.SetCharVar("anUndyingPledgeCS", 0);
                        q.Player.CompleteQuest(QuestLog.Outlands, "AN_UNDYING_PLEDGE");
                        q.Player.DelKeyItem(KeyItem.CaliginousBlade);
                    }
                });
        }
    }
}
