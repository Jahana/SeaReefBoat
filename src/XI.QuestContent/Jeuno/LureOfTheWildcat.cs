// Lure of the Wildcat (Jeuno) (Log ID 3, Quest ID 90), a line-for-line port of
// scripts/quests/jeuno/Lure_of_the_Wildcat_Jeuno.lua (quest rework B3b, Jeuno group 13). Ajithaam : !pos -82 0.1 160
// 244. With ENABLE_TOAU 1, Ajithaam's 10088 gives the White Sentinel Badge and begins it. Accepted: each of twenty NPCs
// in Ru'Lude Gardens, Upper Jeuno, Lower Jeuno and Port Jeuno plays its event once (its bit of Prog unset) and its
// finish sets the bit; Ajithaam answers 10089 at Prog 0, 10090 while bits are missing and 10091 once all twenty are set
// (the badge taken, KEYITEM_LOST, completed: the White Invitation Card). Ajithaam's own script (10176 / 10177, the
// Whitegate trade) reads the completed status and stays.
// The Lua finds an NPC's bit by npc:getName() in its finish (wildcatOnEventFinish); here each zone's csid finish sets
// the bit of the NPC whose event it is, which is the same: within a zone each csid belongs to one NPC (Ru'Lude Gardens'
// 10089 / 10090 / 10091 are Albiona's, Crooked Arrow's and Adolie's; Upper Jeuno's are Ajithaam's, whose 10089 / 10090
// have no finish), and an event's finish gets the NPC that started it. No other NPC script, quest, mission or default
// action starts 10083-10087 / 10091 in Upper Jeuno, 10089-10093 in Ru'Lude Gardens, 10052-10056 in Lower Jeuno or
// 313-317 in Port Jeuno, so the Lua's lookup of another NPC's name (nil: an error) cannot happen. Ru'Lude Gardens has a
// second Crooked_Arrow (17772719), cutscene_only: the engine refuses a talk to it, so the name-keyed handler only ever
// answers the first.
// The Jeuno twin of src/XI.QuestContent/Bastok/LureOfTheWildcat.cs (which holds the shared IsFull).
// Kept as the Lua file has it: the reward is the key item alone (no fame, unlike Bastok's).
using XI.Quests;
using XI.Quests.Authoring;
using BastokWildcat = XI.QuestContent.Bastok.LureOfTheWildcat;

namespace XI.QuestContent.Jeuno
{
    public sealed class LureOfTheWildcat : QuestScript
    {
        // the Lua's wildcatNpcData: the NPC, its bit of Prog and its event
        static readonly (ushort Zone, string Npc, int Bit, int Csid)[] WildcatNpcData =
        {
            (Zone.RuludeGardens, Npc.RuludeGardens.Albiona, 0, 10089),       // !pos -4.363 8.999 -31.901 243
            (Zone.RuludeGardens, Npc.RuludeGardens.CrookedArrow, 1, 10090),  // !pos -34.9 2.999 -10.877 243
            (Zone.RuludeGardens, Npc.RuludeGardens.Muhoho, 2, 10093),        // !pos -4.808 -4.999 27.83 243
            (Zone.RuludeGardens, Npc.RuludeGardens.Adolie, 3, 10091),        // !pos -35 2 59 243
            (Zone.RuludeGardens, Npc.RuludeGardens.Yavoraile, 4, 10092),     // !pos 30.173 2 68.864 243
            (Zone.UpperJeuno, Npc.UpperJeuno.SibilaMobla, 5, 10083),         // !pos -52.531 0 122.68 244
            (Zone.UpperJeuno, Npc.UpperJeuno.Shiroro, 6, 10084),             // !pos -72.632 -1.2 52.905 244
            (Zone.UpperJeuno, Npc.UpperJeuno.LutoMewrilah, 7, 10085),        // !pos -53 0 45 244
            (Zone.UpperJeuno, Npc.UpperJeuno.Renik, 8, 10086),               // !pos -14.672 0 38.624 244
            (Zone.UpperJeuno, Npc.UpperJeuno.Hinda, 9, 10087),               // !pos -25.605 -1.499 19.891 244
            (Zone.LowerJeuno, Npc.LowerJeuno.Sutarara, 10, 10055),           // !pos 30 0.1 -2 245
            (Zone.LowerJeuno, Npc.LowerJeuno.Saprut, 11, 10054),             // !pos 2.257 -5.999 -16.434 245
            (Zone.LowerJeuno, Npc.LowerJeuno.Bluffnix, 12, 10056),           // !pos -43.099 5.9 -114.788 245
            (Zone.LowerJeuno, Npc.LowerJeuno.Naruru, 13, 10053),             // !pos -56 0.1 -138 245
            (Zone.LowerJeuno, Npc.LowerJeuno.Gurdern, 14, 10052),            // !pos -98.021 0 -142.601 245
            (Zone.PortJeuno, Npc.PortJeuno.RedGhost, 15, 314),               // !pos -96.503 0 7.688 246
            (Zone.PortJeuno, Npc.PortJeuno.Karl, 16, 316),                   // !pos -60 0.1 -8 246
            (Zone.PortJeuno, Npc.PortJeuno.Shami, 17, 317),                  // !pos -53.9 0 10.8 246
            (Zone.PortJeuno, Npc.PortJeuno.Rinzei, 18, 315),                 // !pos -14.388 0 -9.156 246
            (Zone.PortJeuno, Npc.PortJeuno.Sagheera, 19, 313),               // !pos -3 0.1 -9 246
        };

        public LureOfTheWildcat() : base(QuestLog.Jeuno, "LURE_OF_THE_WILDCAT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Lure_of_the_Wildcat_Jeuno";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.WhiteInvitationCard,
            });

            // xi.settings.main.ENABLE_TOAU == 1, compared as Lua compares (Settings.MainEquals, quest rework P9): only the
            // number 1; a fraction such as 1.25 is not 1, nor is the boolean true (Lua's `true == 1` is false)
            b.State(QuestStatus.Available, when: q => q.Settings.MainEquals("ENABLE_TOAU", 1))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Ajithaam).OnTrigger(QuestReply.ProgressEvent(10088))
                .OnEventFinish(Zone.UpperJeuno, 10088, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.WhiteSentinelBadge);
                    q.Quest.Begin();
                });

            var accepted = b.State(QuestStatus.Accepted);

            // wildcatOnTrigger / wildcatOnEventFinish, one pair per NPC. The Lua declares the zones in the order Lower
            // Jeuno, Port Jeuno, Ru'Lude Gardens, Upper Jeuno; the lookup keys each handler by zone and NPC (or zone and
            // csid), so the order across zones changes nothing
            foreach (var (zone, npc, bit, csid) in WildcatNpcData)
            {
                accepted
                    .Npc(zone, npc).OnTrigger(q =>
                        !q.Quest.IsVarBitsSet("Prog", bit) ? QuestReply.ProgressEvent(csid) : null)
                    .OnEventFinish(zone, csid, q => q.Quest.SetVarBit("Prog", bit));
            }

            accepted
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Ajithaam).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.Event(10089);
                    }
                    else if (BastokWildcat.IsFull(questProgress, 20))
                    {
                        return QuestReply.ProgressEvent(10091);
                    }
                    else
                    {
                        return QuestReply.Event(10090);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 10091, q =>
                {
                    q.Player.DelKeyItem(KeyItem.WhiteSentinelBadge);
                    q.Text.Special(Text.UpperJeuno.KEYITEM_LOST, KeyItem.WhiteSentinelBadge);

                    q.Quest.Complete();
                });
        }
    }
}
