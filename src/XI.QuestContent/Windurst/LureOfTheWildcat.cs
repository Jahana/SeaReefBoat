// Lure of the Wildcat (Windurst) (Log ID 2, Quest ID 94), a line-for-line port of
// scripts/quests/windurst/Lure_of_the_Wildcat_Windurst.lua (quest rework B3b, Windurst group 12). Ibwam : !pos -25.655
// 1.749 -60.651 241. With ENABLE_TOAU, Ibwam's 736 gives the Green Sentinel Badge and begins it. Accepted: each of twenty
// NPCs in Windurst Woods, Windurst Walls, Windurst Waters and Port Windurst plays its event once (its bit of Prog unset)
// and its finish sets the bit; Ibwam answers 737 at Prog 0, 738 while bits are missing and 739 once all twenty are set
// (the badge taken, KEYITEM_LOST, completed: the Green Invitation Card, fame 0). Ibwam's own script (740 / 793, the
// Whitegate trade 794) reads the completed status and stays. The Lua finds an NPC's bit by npc:getName() in its finish
// (wildcatOnEventFinish); here each zone's csid finish sets the bit of the NPC whose event it is, which is the same:
// within a zone each csid belongs to one NPC, and an event's finish gets the NPC that started it. No other NPC script,
// quest, mission or default action starts 731-735 / 739 in Windurst Woods, 496-500 in Windurst Walls, 936-940 in
// Windurst Waters or 621-625 in Port Windurst (mission 7-2's 731-739 are Windurst Waters'), so the Lua's lookup of
// another NPC's name (nil: an error) cannot happen. The Windurst twin of src/XI.QuestContent/Bastok/LureOfTheWildcat.cs
// (which holds the shared IsFull). Kept as the Lua file has it: the reward's fame is 0 (addFame 0 still runs).
using XI.Quests;
using XI.Quests.Authoring;
using BastokWildcat = XI.QuestContent.Bastok.LureOfTheWildcat;

namespace XI.QuestContent.Windurst
{
    public sealed class LureOfTheWildcat : QuestScript
    {
        // the Lua's wildcatNpcData: the NPC, its bit of Prog and its event
        static readonly (ushort Zone, string Npc, int Bit, int Csid)[] WildcatNpcData =
        {
            (Zone.WindurstWoods, Npc.WindurstWoods.SoniMuni, 0, 735),               // !pos -17.073 1.749 -59.327 241
            (Zone.WindurstWoods, Npc.WindurstWoods.EtsaRhuyuli, 1, 734),            // !pos 62.482 -8.499 -139.836 241
            (Zone.WindurstWoods, Npc.WindurstWoods.CayuPensharhumi, 2, 733),        // !pos 39.437 -0.91 -40.808 241
            (Zone.WindurstWoods, Npc.WindurstWoods.Umumu, 3, 731),                  // !pos 32.575 -5.25 141.372 241
            (Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo, 4, 732),             // !pos 62 -4 240 241
            (Zone.WindurstWalls, Npc.WindurstWalls.Yoriri, 5, 496),                 // !pos 65.268 -8.5 -58.309 239
            (Zone.WindurstWalls, Npc.WindurstWalls.Shantotto, 6, 498),              // !pos 122 -2 112 239
            (Zone.WindurstWalls, Npc.WindurstWalls.MoanMaon, 7, 497),               // !pos 88.244 -6.32 148.912 239
            (Zone.WindurstWalls, Npc.WindurstWalls.Chomomo, 8, 499),                // !pos -1.262 -11 290.224 239
            (Zone.WindurstWalls, Npc.WindurstWalls.NaihArihmepp, 9, 500),           // !pos -64.578 -13.465 202.147 239
            (Zone.WindurstWaters, Npc.WindurstWaters.Npopo, 10, 936),               // !pos -35.464 -5.999 239.12 238
            (Zone.WindurstWaters, Npc.WindurstWaters.LagoCharago, 11, 940),         // !pos -57.271 -11.75 92.503 238
            (Zone.WindurstWaters, Npc.WindurstWaters.AmagusaChigurusa, 12, 937),    // !pos -28.746 -4.5 61.954 238
            (Zone.WindurstWaters, Npc.WindurstWaters.FunpoShipo, 13, 938),          // !pos -44.091 -4.499 41.728 238
            (Zone.WindurstWaters, Npc.WindurstWaters.KyumeRomeh, 14, 939),          // !pos -58 -4 23 238
            (Zone.PortWindurst, Npc.PortWindurst.Kunchichi, 15, 623),               // !pos -115.933 -4.25 109.533 240
            (Zone.PortWindurst, Npc.PortWindurst.YamanHachuman, 16, 624),           // !pos -101.209 -4.25 110.886 240
            (Zone.PortWindurst, Npc.PortWindurst.ChoyiTotlihpa, 17, 622),           // !pos -58.927 -5.732 132.819 240
            (Zone.PortWindurst, Npc.PortWindurst.ThreeofClubs, 18, 625),            // !pos -7.238 -5 106.982 240
            (Zone.PortWindurst, Npc.PortWindurst.Yujuju, 19, 621),                  // !pos 201.523 -4.785 138.978 240
        };

        public LureOfTheWildcat() : base(QuestLog.Windurst, "LURE_OF_THE_WILDCAT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Lure_of_the_Wildcat_Windurst";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 0,
                FameArea = FameArea.Windurst,
                KeyItem = KeyItem.GreenInvitationCard,
            });

            // xi.settings.main.ENABLE_TOAU == 1, compared as Lua compares (Settings.MainEquals, quest rework P9): only the
            // number 1; a fraction such as 1.25 is not 1, nor is the boolean true (Lua's `true == 1` is false)
            b.State(QuestStatus.Available, when: q => q.Settings.MainEquals("ENABLE_TOAU", 1))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Ibwam).OnTrigger(QuestReply.ProgressEvent(736))
                .OnEventFinish(Zone.WindurstWoods, 736, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.GreenSentinelBadge);
                    q.Quest.Begin();
                });

            var accepted = b.State(QuestStatus.Accepted);

            // wildcatOnTrigger / wildcatOnEventFinish, one pair per NPC. The Lua declares the zones in the order Port,
            // Walls, Waters, Woods; the lookup keys each handler by zone and NPC (or zone and csid), so the order across
            // zones changes nothing
            foreach (var (zone, npc, bit, csid) in WildcatNpcData)
            {
                accepted
                    .Npc(zone, npc).OnTrigger(q =>
                        !q.Quest.IsVarBitsSet("Prog", bit) ? QuestReply.ProgressEvent(csid) : null)
                    .OnEventFinish(zone, csid, q => q.Quest.SetVarBit("Prog", bit));
            }

            accepted
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Ibwam).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.Event(737);
                    }
                    else if (BastokWildcat.IsFull(questProgress, 20))
                    {
                        return QuestReply.ProgressEvent(739);
                    }
                    else
                    {
                        return QuestReply.Event(738);
                    }
                })
                .OnEventFinish(Zone.WindurstWoods, 739, q =>
                {
                    q.Player.DelKeyItem(KeyItem.GreenSentinelBadge);
                    q.Text.Special(Text.WindurstWoods.KEYITEM_LOST, KeyItem.GreenSentinelBadge);

                    q.Quest.Complete();
                });
        }
    }
}
