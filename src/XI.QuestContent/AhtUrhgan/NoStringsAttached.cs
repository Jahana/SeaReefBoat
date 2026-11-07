// No Strings Attached (Log ID 6, Quest ID 7), a line-for-line port of
// scripts/quests/ahtUrhgan/No_Strings_Attached.lua (quest rework B3b Aht Urhgan, draft). Bastok Markets: Shamarhaan
// !pos -285.4 -12 -84.7 (no script; default 433). Aht Urhgan Whitegate: Iruki-Waraki !pos 101.3 -6 -29 (script: The
// Wayward Automaton, Operation Teatime; default 259), Ghatsad !pos 34.3 -7.8 57.5 (script: the automaton
// attachments; 256 before this quest is completed). Arrapago Reef: qm10 !pos 457.1 -8.2 60.8 (no script; default
// NOTHING_OUT_OF_ORDINARY). Available from main level ADVANCED_JOB_LEVEL (30): Shamarhaan 434 while Option is 0
// (its finish: Option 1), else 435 (an Event); Iruki-Waraki 260 at Option 1 (its finish begins it), else nil.
// Accepted: Shamarhaan 435 (an Event); Iruki-Waraki 261 (an Event) at Prog 0-3, 266 at Prog 4; Ghatsad 262 at Prog
// 0 (Prog 1), 263 (an Event, once per zone) at Prog 1, 264 at Prog 2 with the Antique Automaton (Prog 3, Timer =
// the next Vana'diel day, the automaton taken), 265 at Prog 3 once Timer <= VanadielUniqueDay() (Prog 4), else nil;
// qm10 214 (a progress cutscene) at Prog 1 (its finish: the Antique Automaton, Prog 2), else
// PILE_OF_DISCARDED_MATERIALS. 266's finish completes it (an Animator, the PUP job gesture, Proud Automaton Owner)
// and, when it does, unlocks Puppetmaster, says YOU_CAN_BECOME_PUP, names the automaton (option + 118) and unlocks
// the Harlequin Frame and Head. Completed: Iruki-Waraki without an Animator anywhere: the name message
// YOU_LOST_YOUR_ANIMATOR, else 267 (an Event); a trade of at least 10000 gil without an Animator: the gil taken, an
// Animator, the name message YOU_BETTER_NOT_LOSE_IT_AGAIN. Kept as the Lua has it: Option stays 1 after the begin;
// 214 gives the automaton whatever the char holds; the gil trade gives the Animator after confirming the gil,
// without checking it fit; 266's name is the option plus 118, unchecked.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class NoStringsAttached : QuestScript
    {
        public NoStringsAttached() : base(QuestLog.AhtUrghan, "NO_STRINGS_ATTACHED") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/No_Strings_Attached";

        // xi.petType.AUTOMATON (scripts/enum/pet_type.lua): no generated class has it
        const int PetTypeAutomaton = 4;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.Animator,
                KeyItem = KeyItem.JobGesturePuppetmaster,
                Title = Title.ProudAutomatonOwner,
            });
            b.Steps(1, 2, 3, 4);

            // Section: Quest available
            b.State(QuestStatus.Available, when: q => q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL"))
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Shamarhaan).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(434); // This CS appears to be bugged on retail and no longer plays
                    }
                    else
                    {
                        return QuestReply.Event(435);
                    }
                })
                .OnEventFinish(Zone.BastokMarkets, 434, q =>
                {
                    q.Quest.SetVar("Option", 1);
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.IrukiWaraki).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 1)
                    {
                        return QuestReply.ProgressEvent(260);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 260, q =>
                {
                    q.Quest.Begin();
                });

            // Section: Quest Accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Shamarhaan).OnTrigger(QuestReply.Event(435))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.IrukiWaraki).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress <= 3)
                    {
                        return QuestReply.Event(261);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.ProgressEvent(266);
                    }
                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ghatsad).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(262);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.Event(263).OncePerZone();
                    }
                    else if (
                        questProgress == 2 &&
                        q.Player.HasKeyItem(KeyItem.AntiqueAutomaton))
                    {
                        return QuestReply.ProgressEvent(264);
                    }
                    else if (
                        questProgress == 3 &&
                        q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(265);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 262, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 264, q =>
                {
                    q.Quest.SetStep(3);
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                    q.Player.DelKeyItem(KeyItem.AntiqueAutomaton);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 265, q =>
                {
                    q.Quest.SetStep(4);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 266, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.UnlockJob(Job.Pup);
                        q.Text.Special(Text.AhtUrhganWhitegate.YOU_CAN_BECOME_PUP);
                        // NEEDS FACET: Player.SetPetName(int petType, int nameId) = player:setPetName(xi.petType.AUTOMATON,
                        // option + 118)
                        q.Player.SetPetName(PetTypeAutomaton, q.Option + 118);
                        // NEEDS FACET: Player.UnlockAttachment(int item) = player:unlockAttachment(xi.item.HARLEQUIN_FRAME)
                        // (its boolean result unused)
                        q.Player.UnlockAttachment(Item.HarlequinFrame);
                        q.Player.UnlockAttachment(Item.HarlequinHead);
                    }
                })
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.qm10).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressCutscene(214);
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.ArrapagoReef.PILE_OF_DISCARDED_MATERIALS);
                    }
                })
                .OnEventFinish(Zone.ArrapagoReef, 214, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.AntiqueAutomaton);
                    q.Quest.SetStep(2);
                });

            // Section: Quest Completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.IrukiWaraki).OnTrade(q =>
                {
                    if (!q.Player.FindItem(Item.Animator) &&
                        q.Trade.HasWithGil(10000))
                    {
                        q.Trade.Confirm();
                        q.Player.GiveItem(Item.Animator);
                        return QuestReply.MessageName(Text.AhtUrhganWhitegate.YOU_BETTER_NOT_LOSE_IT_AGAIN, 0, 0, 0, 0, true, false);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    if (!q.Player.FindItem(Item.Animator))
                    {
                        return QuestReply.MessageName(Text.AhtUrhganWhitegate.YOU_LOST_YOUR_ANIMATOR, Item.Animator, 0, 0, 0, true, false);
                    }
                    else
                    {
                        return QuestReply.Event(267);
                    }
                });
        }
    }
}
