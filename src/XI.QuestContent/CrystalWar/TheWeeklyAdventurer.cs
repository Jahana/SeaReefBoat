// The Weekly Adventurer (Log ID 7, Quest ID 2), a line-for-line port of
// scripts/quests/crystalWar/The_Weekly_Adventurer.lua (quest rework B3b Crystal War, draft). Crawlers' Nest [S]:
// Naiko-Paneiko !pos 294.030 -33.443 -28.637 (no default, no script). Rolanberry Fields [S]: Rakula-Motakula !pos
// -316.786 -12.448 -118.721 (default I_CANNOT_HELP_YOU_NOW). Available: Naiko-Paneiko: 16, whose finish gives the
// Scoop-Dedicated Linkpearl and begins it. Accepted: Naiko-Paneiko: Prog 0 or 4: 21; Prog 1: 19 (171, 1, 2984, 3, 0,
// 10, 0, 0), whose finish takes the linkpearl (silently) and completes it (2000 exp, 2000 gil, the Map of Fort
// Karugo-Narugo); Prog 2 / 3: Prog 4 set at the talk, then 17 / 18. Rakula-Motakula: Prog 0: 3; Prog 4: 3 (0, 0, 0, 0,
// 0, 0, 0, 1); its finish: option 0 (the quiz passed): Prog 1; option 1 (the wrong category): Prog 2; option 2 (wrong
// information): Prog 3. Completed: Naiko-Paneiko: 20 (an Event, replaceDefault). Kept as the Lua has it: 16 begins on
// any option; Prog 4 is written in the talk itself, before 17 / 18 play; the linkpearl is taken before the completion
// (which cannot fail: no item); 3's finish on another option changes nothing; Rakula-Motakula answers nil at Prog 1-3.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class TheWeeklyAdventurer : QuestScript
    {
        public TheWeeklyAdventurer() : base(QuestLog.CrystalWar, "THE_WEEKLY_ADVENTURER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/The_Weekly_Adventurer";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4);

            b.Reward(new QuestReward
            {
                Exp = 2000,
                Gil = 2000,
                KeyItem = KeyItem.MapOfFortKarugoNarugo,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.CrawlersNestS, Npc.CrawlersNestS.NaikoPaneiko).OnTrigger(q =>
                {
                    // Hey there, kid, I'm Naiko-Paneiko, up-and-coming ace journalist
                    return QuestReply.ProgressEvent(16);
                })
                .OnEventFinish(Zone.CrawlersNestS, 16, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.ScoopDedicatedLinkpearl);
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.CrawlersNestS, Npc.CrawlersNestS.NaikoPaneiko).OnTrigger(q =>
                {
                    int prog = q.Quest.Step;

                    // Default or Retry state
                    if (prog == 0 || prog == 4)
                    {
                        // What are you doing, kid? Get to the Rolanberry Fields rightaru away!
                        return QuestReply.ProgressEvent(21);
                    }
                    else if (prog == 1) // Success
                    {
                        // Oh, there you are, kid. Greataru work!
                        return QuestReply.ProgressEvent(19, 171, 1, 2984, 3, 0, 10, 0, 0);
                    }
                    else if (prog == 2) // Fail type 1
                    {
                        // Oooh, look who's back! We got problems, kid? I just don'taru get it.
                        q.Quest.SetStep(4);
                        return QuestReply.ProgressEvent(17);
                    }
                    else if (prog == 3) // Fail type 2
                    {
                        // Oooh, look who's back! We got problems, kid? This just doesn't make any sense!
                        q.Quest.SetStep(4);
                        return QuestReply.ProgressEvent(18);
                    }
                    return null;
                })
                .OnEventFinish(Zone.CrawlersNestS, 19, q =>
                {
                    // SCOOP_DEDICATED_LINKPEARL is removed silently
                    q.Player.DelKeyItem(KeyItem.ScoopDedicatedLinkpearl);
                    q.Quest.Complete();
                })
                // NOTE: Merim-Kurim never interacts with you, despite looking like he does in the CS
                .Npc(Zone.RolanberryFieldsS, Npc.RolanberryFieldsS.RakulaMotakula).OnTrigger(q =>
                {
                    int prog = q.Quest.Step;

                    // NOTE: The quiz answers are: 4-4-5.
                    if (prog == 0)
                    {
                        // These are dangerous lands, adventurer. We advise that you distance yourself from here.
                        return QuestReply.ProgressEvent(3);
                    }
                    else if (prog == 4)
                    {
                        // Oh, it's you again.
                        return QuestReply.ProgressEvent(3, 0, 0, 0, 0, 0, 0, 0, 1);
                    }
                    return null;
                })
                .OnEventFinish(Zone.RolanberryFieldsS, 3, q =>
                {
                    if (q.Option == 0) // Passed the quiz
                    {
                        q.Quest.SetStep(1);
                    }
                    else if (q.Option == 1) // Picked wrong category
                    {
                        q.Quest.SetStep(2);
                    }
                    else if (q.Option == 2) // Reported wrong information
                    {
                        q.Quest.SetStep(3);
                    }
                });

            b.State(QuestStatus.Completed)
                // Alll rightaru! Let's make the news!
                .Npc(Zone.CrawlersNestS, Npc.CrawlersNestS.NaikoPaneiko)
                .OnTrigger(QuestReply.Event(20).ReplaceDefault());
        }
    }
}
