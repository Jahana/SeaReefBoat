// Catch It If You Can (Log ID 2, Quest ID 21), ported from the quest branches of
// scripts/zones/Port_Windurst/npcs/Ohruru.lua (quest rework B3b, Windurst group 21; call-only). Ohruru : !pos -108 -5
// 94 240. No default action, and no other quest answers him. His one elseif chain holds Wonder Wands' lines around this
// quest's (258 first, 265 after this quest's arms), so they are here; his standard 230 (the chain's else) stays in his
// script.
// Wonder Wands accepted: 258 (0, Rose Wand), before every arm of this quest. Available: QuestCatchItIfYouCan_var 0:
// 230, var 1; var 1: 253, var 2; var 2: 231 (its finish, any option: begun); another var (a GM): nothing. Accepted or
// completed under Mute, Bane or Plague: 246; else with needToZone: 255. Accepted, none of the three: 248 or 251 by
// math.randomInt(1, 2). Wonder Wands completed: 265. Else his 230.
// 246's finish, option 0: needToZone; Mute taken and 1000 gil, else Bane and 1200, else Plague and 1500
// (npcUtil.giveCurrency); var 0; accepted: completed, then Windurst fame 10; else fame 10.
// Not ported, as no input reads them: the Lua's `catch >= 1` operands of 246 and 255 (the available arm before them
// answers every available char, with an event or with nothing), and 248 / 251's three `not hasStatusEffect` operands
// (the 246 arm before them takes every char with one of the effects).
// Kept as the Lua has it: 231 begins on any option; the first two talks set the var in onTrigger (here before the event
// starts, in Lua after it: no packet goes between them); a var past 2 while available plays nothing (his standard is
// not reached); 246's finish reads the effects again (with none left only needToZone, var 0 and the fame) and acts on
// option 0 only; Wonder Wands accepted answers before this quest's arms, Wonder Wands completed after them.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class CatchItIfYouCan : QuestScript
    {
        const string Var = "QuestCatchItIfYouCan_var";

        // xi.effect (no generated constants)
        const int Mute = 29;
        const int Bane = 30;
        const int Plague = 31;

        public CatchItIfYouCan() : base(QuestLog.Windurst, "CATCH_IT_IF_YOU_CAN") { }

        static bool HasAnEffect(IQuestCheck q) =>
            q.Player.HasStatusEffect(Mute) || q.Player.HasStatusEffect(Bane) || q.Player.HasStatusEffect(Plague);

        protected override void Build(QuestScriptBuilder b)
        {
            // Ohruru.lua onTrigger: Wonder Wands accepted
            b.AnyState(when: q => q.Player.QuestStatus(QuestLog.Windurst, "WONDER_WANDS") == QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Ohruru).Called()
                    .OnTrigger(QuestReply.Event(258, 0, Item.RoseWand));

            // available: by QuestCatchItIfYouCan_var
            b.State(QuestStatus.Available)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Ohruru).Called().OnTrigger(q =>
                {
                    int prog = q.Player.CharVar(Var);

                    if (prog == 0)
                    {
                        q.Player.SetCharVar(Var, 1);
                        return QuestReply.Event(230); // before the quest 1
                    }
                    else if (prog == 1)
                    {
                        q.Player.SetCharVar(Var, 2);
                        return QuestReply.Event(253); // before the start
                    }
                    else if (prog == 2)
                    {
                        return QuestReply.Event(231); // before the quest 2
                    }

                    // the inner if has no else: nothing plays
                    return QuestReply.NoAction();
                });

            // accepted or completed, Mute, Bane or Plague: the turn in
            b.AnyState(when: HasAnEffect)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Ohruru).Called().OnTrigger(QuestReply.Event(246));

            // accepted or completed, needToZone: after the quest
            b.AnyState(when: q => q.Player.NeedToZone)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Ohruru).Called().OnTrigger(QuestReply.Event(255));

            // accepted (none of the three): during the quest
            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Ohruru).Called()
                    .OnTrigger(q => q.Player.RandomInt(1, 2) == 1 ? QuestReply.Event(248) : QuestReply.Event(251));

            // Wonder Wands completed
            b.AnyState(when: q => q.Player.QuestStatus(QuestLog.Windurst, "WONDER_WANDS") == QuestStatus.Completed)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Ohruru).Called().OnTrigger(QuestReply.Event(265));

            // Ohruru.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.PortWindurst, 231, q => q.Quest.Begin())
                .OnEventFinish(Zone.PortWindurst, 246, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.SetNeedToZone(true);

                        if (q.Player.HasStatusEffect(Mute))
                        {
                            q.Player.DelStatusEffect(Mute);
                            q.Player.GiveCurrency("gil", 1000);
                        }
                        else if (q.Player.HasStatusEffect(Bane))
                        {
                            q.Player.DelStatusEffect(Bane);
                            q.Player.GiveCurrency("gil", 1200);
                        }
                        else if (q.Player.HasStatusEffect(Plague))
                        {
                            q.Player.DelStatusEffect(Plague);
                            q.Player.GiveCurrency("gil", 1500);
                        }

                        q.Player.SetCharVar(Var, 0);

                        if (q.Quest.Status == QuestStatus.Accepted)
                        {
                            // the raw player:completeQuest: not quest:complete, whose cleanup would clear the
                            // quest's own Quest[2][21] vars
                            q.Player.CompleteQuest(QuestLog.Windurst, "CATCH_IT_IF_YOU_CAN");
                            q.Player.AddFame(FameArea.Windurst, 10);
                        }
                        else
                        {
                            q.Player.AddFame(FameArea.Windurst, 10);
                        }
                    }
                });
        }
    }
}
