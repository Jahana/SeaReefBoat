// Onion Rings (Log ID 2, Quest ID 42), a line-for-line port of scripts/quests/windurst/SOB4_Onion_Rings.lua (quest rework
// B3b). Kohlo-Lakolo : !pos -26.8 -6 190 240. Once Inspector's Gadget is completed, at level 5 and Windurst fame level 3:
// without the Old Ring, Kohlo-Lakolo's 429 starts a one-Vana'diel-day timer while the quest is still available (Prog 1,
// EndTime); with the ring, 430 begins it when the timer runs and Prog is 1, else 432 completes it at once (fame 30, Star
// Onion Brigadier; the ring taken). Accepted at Prog 1: each of the brigade plays an in-time line or a late one; late,
// Kohlo-Lakolo's 433 completes it; in time, the House of the Hero's 289 completes it. Completed while Crying Over Onions
// is available: the brigade's new defaults (replaceDefault). Kept as the Lua file has it: the offer reads this quest's
// char-var mustZone, which nothing sets; the completions set the player local var '[2][76]mustZone', which nothing
// reads; 429 plays again at Prog 1 without the ring (the timer is not restarted); the timer is VanadielTime(), earth
// seconds, and a Vana'diel day is 3456 of them.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class OnionRings : QuestScript
    {
        // xi.vanaTime.DAY (scripts/enum/vana_time.lua): 24 * 60 * 2.4 earth seconds
        const int VanaTimeDay = 3456;

        public OnionRings() : base(QuestLog.Windurst, "ONION_RINGS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/SOB4_Onion_Rings";

        static QuestReply TimedEvents(IQuestContext q, int inTime, int outATime)
        {
            int currentTime = q.Player.VanadielTime;
            int endTime = q.Quest.Var("EndTime");

            if (currentTime < endTime)
            {
                return QuestReply.Event(inTime);
            }
            else
            {
                return QuestReply.Event(outATime);
            }
        }

        // 432, 433 and 289 (the same body in Lua)
        static void CompleteQuest(IQuestContext q)
        {
            if (q.Quest.Complete())
            {
                q.Player.DelKeyItem(KeyItem.OldRing);
                q.Player.SetLocalVar("[2][76]mustZone", 1);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Windurst, Title = Title.StarOnionBrigadier });

            // NOTE
            // From the moment you speak with Kohlo-Lakolo, if you dont have the Old Ring, a 24 hour time limit will start.
            // You still need to get the ring, no matter what.

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Windurst, "INSPECTORS_GADGET"))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo).OnTrigger(q =>
                {
                    if (q.Player.MainLevel >= 5 &&
                        q.Player.FameLevel(FameArea.Windurst) >= 3 &&
                        !q.Quest.MustZone)
                    {
                        if (q.Player.HasKeyItem(KeyItem.OldRing))
                        {
                            int currentTime = q.Player.VanadielTime;
                            int endTime = q.Quest.Var("EndTime");

                            if (currentTime < endTime &&
                                q.Quest.Step == 1)
                            {
                                return QuestReply.ProgressEvent(430, 0, KeyItem.OldRing); // Quest starting event.
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(432, 0, KeyItem.OldRing); // Instant Quest Complete.
                            }
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(429); // Timer start and reminder text.
                        }
                    }
                    else
                    {
                        return QuestReply.Event(422); // Default text.
                    }
                })
                .OnEventFinish(Zone.PortWindurst, 429, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                        q.Quest.SetVar("EndTime", q.Player.VanadielTime + VanaTimeDay); // Set current quest started variables.
                    }
                })
                .OnEventFinish(Zone.PortWindurst, 430, q => q.Quest.Begin())
                .OnEventFinish(Zone.PortWindurst, 432, CompleteQuest);

            // Section: Quest accepeted. You didn't start with the "Old Ring" Key Item.
            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 1)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo).OnTrigger(q => TimedEvents(q, 431, 433))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(q => TimedEvents(q, 435, 442))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(q => TimedEvents(q, 434, 441))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(q => TimedEvents(q, 438, 445))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(q => TimedEvents(q, 436, 443))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Shanruru).OnTrigger(q => TimedEvents(q, 439, 446))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(q => TimedEvents(q, 437, 444))
                .OnEventFinish(Zone.PortWindurst, 433, CompleteQuest)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls._6n2).OnTrigger(q =>
                {
                    int currentTime = q.Player.VanadielTime;
                    int endTime = q.Quest.Var("EndTime");

                    if (currentTime < endTime)
                    {
                        return QuestReply.ProgressEvent(289);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWalls, 289, CompleteQuest);

            // Section: Quest completed.
            b.State(QuestStatus.Completed, when: q =>
                    q.Player.QuestStatus(QuestLog.Windurst, "CRYING_OVER_ONIONS") == QuestStatus.Available)
                // New default texts.
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(QuestReply.Event(442).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(QuestReply.Event(441).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(QuestReply.Event(445).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(QuestReply.Event(443).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Shanruru).OnTrigger(QuestReply.Event(446).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(QuestReply.Event(444).ReplaceDefault());
        }
    }
}
