// Community Service (Log ID 3, Quest ID 15), a line-for-line port of scripts/quests/jeuno/Community_Service.lua (quest
// rework B3b). Zauko and the twelve Streetlamps (_l00-_l11) in Lower Jeuno; one section for every status while
// vars.Prog >= 0 (a GM's Prog below 0: no section, Zauko's default 118 alone, the lamps nothing).
// From 18:00 to 22:59 the first char of the night to answer Zauko's 116 (its update re-checks the hour and the zone's
// commServiceStart) begins it: Prog 1, Count 12, Timer = today, the zone's commServiceStart 1 and its name in
// commServicePlayer1-4 (updateEvent 1, else 0). A lamp plays 120-131 with a parameter (1 light it, 2 lit, 3 too late, 5
// lit, 6 examine); option 1 lights it (OPEN_DOOR, Count - 1, YOU_LIGHT_THE_LAMP and the count left; the twelfth:
// LAMP_MSG_OFFSET, Prog 2, the zone's allLightsLit 1). Zauko then plays 117: completed (Torchbearer), fame +7 x3, the
// zone's commServiceComp 1 and commServicePlayer = the char's id; option 1 gives the Lamp Lighter's Membership Card.
// Past 1:00 the next day he plays 119 (Prog 0); else 114 on the quest; 113 once someone finished tonight or lit every
// lamp; from 5:00 to 17:59 a card holder who was not asked today gets 118 (Wait = today; option 1 returns the card).
// Kept: the one section matches every status (Prog 0 or more), so Zauko's default 118 (the quest's own csid) runs the 118 finish when
// the quest returns nil; lightLamp does not check Prog; the 119 finish clears a char var named Option (player:setVar),
// not a quest var. The three xi.quest.communityService* functions the Lua file also defined (the 1:00, 5:00 and 18:00
// rounds of Lower Jeuno's onGameHour) are zone logic: they moved, unchanged, into scripts/zones/Lower_Jeuno/Zone.lua.
using System.Text;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class CommunityService : QuestScript
    {
        public CommunityService() : base(QuestLog.Jeuno, "COMMUNITY_SERVICE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Community_Service";

        // xi.animation (scripts/enum/animation.codegen.lua)
        const int OpenDoor = 8;
        const int CloseDoor = 9;

        // Lights the lamps
        static void LightLamp(IQuestContext q)
        {
            if (q.Option == 1)
            {
                int remaining = q.Quest.Var("Count") - 1;
                q.Quest.SetVar("Count", remaining);
                q.Npc.SetAnimation(OpenDoor);

                if (remaining == 0)
                {
                    q.Text.Special(Text.LowerJeuno.YOU_LIGHT_THE_LAMP);
                    q.Text.Special(Text.LowerJeuno.LAMP_MSG_OFFSET);
                    q.Quest.SetStep(2);
                    q.Player.SetZoneLocalVar("allLightsLit", 1);
                }
                else
                {
                    q.Text.Special(Text.LowerJeuno.YOU_LIGHT_THE_LAMP);
                    q.Text.Special(q.Text.Id(Text.LowerJeuno.LAMP_MSG_OFFSET) + 1, remaining);
                }
            }
        }

        // Determines which parameter is passed when a player checks a light; null where the Lua returns nil
        static int? GetLampParam(IQuestContext q)
        {
            int npcAnimation = q.Npc.Animation;

            // In progress.
            if (q.Quest.Step == 1)
            {
                int hour = q.Player.VanadielHour;
                int date = q.Player.VanadielUniqueDay;
                int timer = q.Quest.Var("Timer");
                if ((timer == date && hour >= 18) || // Same day flagged
                    (timer + 1 == date && hour < 1)) // Day after it was flagged
                {
                    if (npcAnimation == OpenDoor)
                    {
                        return 2; // The lamp is already lit.
                    }
                    else if (npcAnimation == CloseDoor)
                    {
                        return 1; // Light the lamp? Yes/No
                    }
                }
                else if ((timer + 1 < date) ||
                         (timer + 1 == date && hour >= 1))
                {
                    return 3;
                }
            }

            // The lamp is lit.
            else if (npcAnimation == OpenDoor)
            {
                return 5;
            }

            // You examine the lamp. It seems that it must be lit manually.
            else
            {
                return 6;
            }

            return null;
        }

        // quest:progressEvent(csid, getLampParam(player, npc), lamp): startEvent(csid, nil, lamp) sends the nil as 0 and
        // keeps the lamp after it (community_service#gm_timer: 122 with [0, 2]), so a nil parameter is a 0 here
        static QuestReply LampEvent(IQuestContext q, int csid, int lamp) =>
            QuestReply.ProgressEvent(csid, GetLampParam(q) ?? 0, lamp);

        // Stores the player name to be called lader as a string (player:getName(): a char's name is ASCII)
        static void EncodeToVars(IQuestContext q)
        {
            byte[] pName = Encoding.ASCII.GetBytes(q.Player.Name);

            int[] stored = { 0, 0, 0, 0 };
            int chunk = 1;

            for (int i = 1; i <= pName.Length; i += 4)
            {
                int v = 0;

                for (int j = 1; j <= 4; j++)
                {
                    int idx = i + j - 1;
                    if (idx > pName.Length)
                    {
                        break;
                    }

                    int c = pName[idx - 1];
                    v = v | (c << ((j - 1) * 8));
                }

                stored[chunk - 1] = v;
                chunk = chunk + 1;
                if (chunk > 4)
                {
                    break;
                }
            }

            for (int i = 1; i <= stored.Length; i++)
            {
                q.Player.SetZoneLocalVar("commServicePlayer" + i, stored[i - 1]);
            }
        }

        // Returns the stored player name and concats it together into a single string
        static string DecodeFromVars(IQuestContext q)
        {
            var chars = new StringBuilder();

            for (int chunk = 1; chunk <= 4; chunk++)
            {
                int v = q.Player.ZoneLocalVar("commServicePlayer" + chunk);
                if (v == 0)
                {
                    break;
                }

                for (int j = 1; j <= 4; j++)
                {
                    int c = (v >> ((j - 1) * 8)) & 0xFF;
                    if (c == 0)
                    {
                        return chars.ToString();
                    }

                    chars.Append((char)c);
                }
            }

            return chars.ToString();
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.Torchbearer });
            b.Steps(1, 2);

            b.AnyState(when: q => q.Quest.Step >= 0)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Zauko).OnTrigger(q =>
                {
                    // local zone = player:getZone(); if not zone then return end: a char in Lower Jeuno always has its
                    // zone, so the guard is not ported (the zone's local vars are Player.ZoneLocalVar / SetZoneLocalVar)
                    int date = q.Player.VanadielUniqueDay;
                    int doneCommService = q.Player.QuestStatus(QuestLog.Jeuno, "COMMUNITY_SERVICE") == QuestStatus.Completed ? 1 : 0;
                    int hour = q.Player.VanadielHour;
                    int member = q.Player.HasKeyItem(KeyItem.LampLightersMembershipCard) ? 1 : 0;
                    int progress = q.Quest.Step;
                    int questStarted = q.Player.ZoneLocalVar("commServiceStart"); // Either 1 or 0
                    int questCompleted = q.Player.ZoneLocalVar("commServiceComp"); // Either 1 or 0
                    int timer = q.Quest.Var("Timer");
                    string playerString = DecodeFromVars(q);

                    // Offers the quest
                    if (hour >= 18 &&
                        hour < 23 &&
                        questStarted == 0) // Only 1 person per night can flag the quest
                    {
                        return QuestReply.ProgressEvent(116, doneCommService);
                    }

                    // Completes the quest
                    else if (progress == 2)
                    {
                        return QuestReply.ProgressEvent(117, doneCommService, member); // 0 0 = First time, 1 0 Quest completed before and does not have KI., 1 1 Has completed quest and has KI
                    }

                    // Player has the quest flagged and failed
                    else if (progress == 1 &&
                             ((timer + 1 == date && hour >= 1) || // Day after quest flagged
                              (timer + 1 < date))) // Day after it was flagged or later
                    {
                        return QuestReply.ProgressEvent(119);
                    }

                    // Player is on the quest and has not failed/completed it yet
                    else if (progress == 1)
                    {
                        return QuestReply.Event(114);
                    }

                    // Someone has completed the quest today and turned it in
                    else if (questCompleted == 1)
                    {
                        if (q.Player.ZoneLocalVar("commServicePlayer") == q.Player.Id)
                        {
                            return QuestReply.Event(113);
                        }
                        else
                        {
                            return QuestReply.Event(113).Option(0, questCompleted).OptionString(0, playerString);
                        }
                    }

                    // Another player is on the quest and has lit all the lamps but has not turned it in yet
                    else if (q.Player.ZoneLocalVar("allLightsLit") == 1)
                    {
                        return QuestReply.Event(113);
                    }

                    // Asks a player with the KI if they would like to keep it. Only asks once per day.
                    else if (hour >= 5 &&
                             hour < 18 &&
                             q.Player.HasKeyItem(KeyItem.LampLightersMembershipCard) &&
                             q.Quest.Var("Wait") < q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.Event(118, 1);
                    }
                    return null;
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.l00).OnTrigger(q => LampEvent(q, 120, 0))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.l01).OnTrigger(q => LampEvent(q, 121, 1))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.l02).OnTrigger(q => LampEvent(q, 122, 2))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.l03).OnTrigger(q => LampEvent(q, 123, 3))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.l04).OnTrigger(q => LampEvent(q, 124, 4))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.l05).OnTrigger(q => LampEvent(q, 125, 5))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.l06).OnTrigger(q => LampEvent(q, 126, 6))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.l07).OnTrigger(q => LampEvent(q, 127, 7))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.l08).OnTrigger(q => LampEvent(q, 128, 8))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.l09).OnTrigger(q => LampEvent(q, 129, 9))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.l10).OnTrigger(q => LampEvent(q, 130, 10))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.l11).OnTrigger(q => LampEvent(q, 131, 11))
                .OnEventUpdate(Zone.LowerJeuno, 116, q =>
                {
                    // the zone nil guard: as in Zauko's trigger
                    int hour = q.Player.VanadielHour;
                    int doneCommService = q.Player.QuestStatus(QuestLog.Jeuno, "COMMUNITY_SERVICE") == QuestStatus.Completed ? 1 : 0;
                    int questStarted = q.Player.ZoneLocalVar("commServiceStart"); // Either 1 or 0

                    // Verifies if no one has accepted the quest/time has changed to too late
                    if (hour >= 18 &&
                        hour < 23 &&
                        questStarted == 0)
                    {
                        EncodeToVars(q);
                        q.Player.SetZoneLocalVar("commServiceStart", 1);
                        q.Quest.Begin();
                        q.Quest.SetStep(1);
                        q.Quest.SetVar("Count", 12);
                        q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay);
                        q.Player.UpdateEvent(1, doneCommService);
                    }
                    else
                    {
                        q.Player.UpdateEvent(0, doneCommService); // Someone else flagged this before they hit ok, or time ticked past the cut off.
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 117, q =>
                {
                    q.Player.SetZoneLocalVar("commServiceComp", 1);
                    q.Player.SetZoneLocalVar("commServicePlayer", q.Player.Id);
                    q.Quest.Complete();
                    q.Player.AddFame(FameArea.Sandoria, 7);
                    q.Player.AddFame(FameArea.Bastok, 7);
                    q.Player.AddFame(FameArea.Windurst, 7);

                    if (q.Option == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.LampLightersMembershipCard);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 118, q =>
                {
                    q.Quest.SetVar("Wait", q.Player.VanadielUniqueDay);
                    if (q.Option == 1)
                    {
                        q.Player.DelKeyItem(KeyItem.LampLightersMembershipCard);
                        q.Text.Special(Text.LowerJeuno.YOU_RETURN_THE, KeyItem.LampLightersMembershipCard);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 119, q =>
                {
                    q.Quest.SetStep(0);
                    q.Player.SetCharVar("Option", 0); // Clears the local var incase the player never zones after failing
                })
                .OnEventFinish(Zone.LowerJeuno, 120, LightLamp)
                .OnEventFinish(Zone.LowerJeuno, 121, LightLamp)
                .OnEventFinish(Zone.LowerJeuno, 122, LightLamp)
                .OnEventFinish(Zone.LowerJeuno, 123, LightLamp)
                .OnEventFinish(Zone.LowerJeuno, 124, LightLamp)
                .OnEventFinish(Zone.LowerJeuno, 125, LightLamp)
                .OnEventFinish(Zone.LowerJeuno, 126, LightLamp)
                .OnEventFinish(Zone.LowerJeuno, 127, LightLamp)
                .OnEventFinish(Zone.LowerJeuno, 128, LightLamp)
                .OnEventFinish(Zone.LowerJeuno, 129, LightLamp)
                .OnEventFinish(Zone.LowerJeuno, 130, LightLamp)
                .OnEventFinish(Zone.LowerJeuno, 131, LightLamp);
        }
    }
}
