// Lure of the Wildcat (Bastok) (Log ID 1, Quest ID 84), a line-for-line port of
// scripts/quests/bastok/Lure_of_the_Wildcat_Bastok.lua (quest rework B3b). Alib-Mufalib : !pos 116.08 7.372 -31.82
// 236. With ENABLE_TOAU, Alib-Mufalib's 357 gives the Blue Sentinel Badge and begins it. Accepted: each of twenty NPCs
// in Port Bastok, the Metalworks, Bastok Markets and Bastok Mines plays its event once (its bit of Prog unset) and its
// finish sets the bit; Alib-Mufalib answers 358 at Prog 0, 359 while bits are missing and 360 once all twenty are set
// (the badge taken, KEYITEM_LOST, completed: the Blue Invitation Card, fame 0). Alib-Mufalib's own script (361 / 378,
// the Whitegate trade) reads the completed status and stays.
// The Lua finds an NPC's bit by npc:getName() in its finish (wildcatOnEventFinish); here each csid's finish sets the
// bit of the NPC whose event it is, which is the same: each csid belongs to one NPC, and an event's finish gets the NPC
// that started it. Kept as the Lua file has it: the reward's fame is 0 (addFame 0 still runs).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class LureOfTheWildcat : QuestScript
    {
        // the Lua's wildcatNpcData: the NPC, its bit of Prog and its event
        static readonly (ushort Zone, string Npc, int Bit, int Csid)[] WildcatNpcData =
        {
            (Zone.PortBastok, Npc.PortBastok.Kaede, 0, 352),                // !pos 48 -6 67 236
            (Zone.PortBastok, Npc.PortBastok.PatientWheel, 1, 354),         // !pos -107.988 3.898 52.557 236
            (Zone.PortBastok, Npc.PortBastok.Paujean, 2, 355),              // !pos -93.738 4.649 34.373 236
            (Zone.PortBastok, Npc.PortBastok.Hilda, 3, 356),                // !pos -163 -8 13 236
            (Zone.PortBastok, Npc.PortBastok.Tilian, 4, 353),               // !pos -118.46 4.999 -68.09 236
            (Zone.Metalworks, Npc.Metalworks.Raibaht, 5, 933),              // !pos -27 -10 -1 237
            (Zone.Metalworks, Npc.Metalworks.InvincibleShield, 6, 932),     // !pos -51.083 -11 2.126 237
            (Zone.Metalworks, Npc.Metalworks.Manilam, 7, 931),              // !pos -57.3 -11 22.332 237
            (Zone.Metalworks, Npc.Metalworks.Kaela, 8, 934),                // !pos 40.167 -14.999 16.073 237
            (Zone.Metalworks, Npc.Metalworks.Ayame, 9, 935),                // !pos 133 -19 34 237
            (Zone.BastokMarkets, Npc.BastokMarkets.Harmodios, 10, 430),     // !pos -79.928 -4.824 -135.114 235
            (Zone.BastokMarkets, Npc.BastokMarkets.Arawn, 11, 429),         // !pos -121.492 -4 -123.923 235
            (Zone.BastokMarkets, Npc.BastokMarkets.Horatius, 12, 428),      // !pos -158 -6 -117 235
            (Zone.BastokMarkets, Npc.BastokMarkets.Ken, 13, 432),           // !pos -340.857 -11.003 -149.008 235
            (Zone.BastokMarkets, Npc.BastokMarkets.Pavel, 14, 431),         // !pos -349.798 -10.002 -181.296 235
            (Zone.BastokMines, Npc.BastokMines.Griselda, 15, 507),          // !pos -25.749 -0.044 52.36 234
            (Zone.BastokMines, Npc.BastokMines.Goraow, 16, 506),            // !pos 38 0.1 14 234
            (Zone.BastokMines, Npc.BastokMines.EchoHawk, 17, 505),          // !pos -0.965 5.999 -15.567 234
            (Zone.BastokMines, Npc.BastokMines.Deidogg, 18, 504),           // !pos -13 7 29 234
            (Zone.BastokMines, Npc.BastokMines.Vaghron, 19, 503),           // !pos -39.162 -1 -92.147 234
        };

        public LureOfTheWildcat() : base(QuestLog.Bastok, "LURE_OF_THE_WILDCAT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Lure_of_the_Wildcat_Bastok";

        // utils.mask.isFull(mask, len): the low len bits all set. Public: the San d'Oria, Jeuno and Windurst Wildcats, Flyers
        // for Regine and Outlands' Mama Mia use it too
        public static bool IsFull(int mask, int len)
        {
            int fullMask = (1 << len) - 1;

            return (mask & fullMask) == fullMask;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 0,
                FameArea = FameArea.Bastok,
                KeyItem = KeyItem.BlueInvitationCard,
            });

            // xi.settings.main.ENABLE_TOAU == 1, compared as Lua compares (Settings.MainEquals, quest rework P9): only the
            // number 1; a fraction such as 1.25 is not 1, nor is the boolean true (Lua's `true == 1` is false)
            b.State(QuestStatus.Available, when: q => q.Settings.MainEquals("ENABLE_TOAU", 1))
                .Npc(Zone.PortBastok, Npc.PortBastok.AlibMufalib).OnTrigger(QuestReply.ProgressEvent(357))
                .OnEventFinish(Zone.PortBastok, 357, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.BlueSentinelBadge);
                    q.Quest.Begin();
                });

            var accepted = b.State(QuestStatus.Accepted);

            // wildcatOnTrigger / wildcatOnEventFinish, one pair per NPC
            foreach (var (zone, npc, bit, csid) in WildcatNpcData)
            {
                accepted
                    .Npc(zone, npc).OnTrigger(q =>
                        !q.Quest.IsVarBitsSet("Prog", bit) ? QuestReply.ProgressEvent(csid) : null)
                    .OnEventFinish(zone, csid, q => q.Quest.SetVarBit("Prog", bit));
            }

            accepted
                .Npc(Zone.PortBastok, Npc.PortBastok.AlibMufalib).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.Event(358);
                    }
                    else if (IsFull(questProgress, 20))
                    {
                        return QuestReply.ProgressEvent(360);
                    }
                    else
                    {
                        return QuestReply.Event(359);
                    }
                })
                .OnEventFinish(Zone.PortBastok, 360, q =>
                {
                    q.Player.DelKeyItem(KeyItem.BlueSentinelBadge);
                    q.Text.Special(Text.PortBastok.KEYITEM_LOST, KeyItem.BlueSentinelBadge);

                    q.Quest.Complete();
                });
        }
    }
}
