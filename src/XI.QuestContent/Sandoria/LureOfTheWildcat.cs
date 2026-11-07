// Lure of the Wildcat (San d'Oria) (Log ID 0, Quest ID 113), a line-for-line port of
// scripts/quests/sandoria/Lure_of_the_Wildcat_Sandoria.lua (quest rework B3b, San d'Oria group 10). Amutiyaal : !pos
// 116 0.1 84 230. With ENABLE_TOAU, Amutiyaal's 812 gives the Red Sentinel Badge and begins it. Accepted: each of
// twenty NPCs in Southern and Northern San d'Oria, Port San d'Oria and Chateau d'Oraguille plays its event once (its
// bit of Prog unset) and its finish sets the bit; Amutiyaal answers 813 at Prog 0, 814 while bits are missing and 815
// once all twenty are set (the badge taken, KEYITEM_LOST, completed: the Red Invitation Card, fame 0). Amutiyaal's own
// script (816 / 880, the Whitegate trade) reads the completed status and stays. The Lua finds an NPC's bit by
// npc:getName() in its finish (wildcatOnEventFinish); here each zone's csid finish sets the bit of the NPC whose event
// it is, which is the same: within a zone each csid belongs to one NPC (807, 808 and 809 are used in both Southern and
// Northern San d'Oria, by different NPCs), and an event's finish gets the NPC that started it. No other NPC script,
// quest, mission or default action starts 807-811 / 815 in Southern San d'Oria, 805-809 in Northern, 746-750 in Port
// San d'Oria or 558-562 in the Chateau, so the Lua's lookup of another NPC's name (nil: an error) cannot happen. The
// San d'Oria twin of src/XI.QuestContent/Bastok/LureOfTheWildcat.cs (which holds the shared IsFull). Kept as the Lua
// file has it: the reward's fame is 0 (addFame 0 still runs).
using XI.Quests;
using XI.Quests.Authoring;
using BastokWildcat = XI.QuestContent.Bastok.LureOfTheWildcat;

namespace XI.QuestContent.Sandoria
{
    public sealed class LureOfTheWildcat : QuestScript
    {
        // the Lua's wildcatNpcData: the NPC, its bit of Prog and its event
        static readonly (ushort Zone, string Npc, int Bit, int Csid)[] WildcatNpcData =
        {
            (Zone.SouthernSanDoria, Npc.SouthernSanDoria.Daggao, 0, 810),             // !pos 89 0 119 230
            (Zone.SouthernSanDoria, Npc.SouthernSanDoria.Authere, 1, 809),            // !pos 33 1 -31 230
            (Zone.SouthernSanDoria, Npc.SouthernSanDoria.Rouva, 2, 808),              // !pos -17 2 10 230
            (Zone.SouthernSanDoria, Npc.SouthernSanDoria.Femitte, 3, 807),            // !pos -17 2 10 230
            (Zone.SouthernSanDoria, Npc.SouthernSanDoria.Deraquien, 4, 811),          // !pos -98 -2 31 230
            (Zone.NorthernSanDoria, Npc.NorthernSanDoria.Giaunne, 5, 805),            // !pos -13 0 36 231
            (Zone.NorthernSanDoria, Npc.NorthernSanDoria.Anilla, 6, 808),             // !pos 8 0.1 61 231
            (Zone.NorthernSanDoria, Npc.NorthernSanDoria.Maloquedil, 7, 807),         // !pos 35 0.1 60 231
            (Zone.NorthernSanDoria, Npc.NorthernSanDoria.Phairupegiont, 8, 806),      // !pos -46 0.1 76 231
            (Zone.NorthernSanDoria, Npc.NorthernSanDoria.Bertenont, 9, 809),          // !pos -165 0.1 226 231
            (Zone.PortSanDoria, Npc.PortSanDoria.Perdiouvilet, 10, 750),              // !pos -59 -5 -29 232
            (Zone.PortSanDoria, Npc.PortSanDoria.Pomilla, 11, 749),                   // !pos -38 -4 -55 232
            (Zone.PortSanDoria, Npc.PortSanDoria.Cherlodeau, 12, 748),                // !pos -20 -4 -69 232
            (Zone.PortSanDoria, Npc.PortSanDoria.Parcarin, 13, 747),                  // !pos -9 -13 -151 232
            (Zone.PortSanDoria, Npc.PortSanDoria.Rugiette, 14, 746),                  // !pos 71 -9 -73 232
            (Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla, 15, 562),         // !pos 27 0.1 0.1 233
            (Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver, 16, 558),          // !pos 2 0.1 0.1 233
            (Zone.ChateauDoraguille, Npc.ChateauDoraguille.Rahal, 17, 559),           // !pos -28 0.1 -6 233
            (Zone.ChateauDoraguille, Npc.ChateauDoraguille.Perfaumand, 18, 560),      // !pos -39 -3 69 233
            (Zone.ChateauDoraguille, Npc.ChateauDoraguille.Chalvatot, 19, 561),       // !pos -105 0.1 72 233
        };

        public LureOfTheWildcat() : base(QuestLog.Sandoria, "LURE_OF_THE_WILDCAT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Lure_of_the_Wildcat_Sandoria";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 0,
                FameArea = FameArea.Sandoria,
                KeyItem = KeyItem.RedInvitationCard,
            });

            // xi.settings.main.ENABLE_TOAU == 1, compared as Lua compares (Settings.MainEquals, quest rework P9): only the
            // number 1; a fraction such as 1.25 is not 1, nor is the boolean true (Lua's `true == 1` is false)
            b.State(QuestStatus.Available, when: q => q.Settings.MainEquals("ENABLE_TOAU", 1))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Amutiyaal).OnTrigger(QuestReply.ProgressEvent(812))
                .OnEventFinish(Zone.SouthernSanDoria, 812, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.RedSentinelBadge);
                    q.Quest.Begin();
                });

            var accepted = b.State(QuestStatus.Accepted);

            // wildcatOnTrigger / wildcatOnEventFinish, one pair per NPC. The Lua declares the zones in the order Chateau,
            // Northern, Port, Southern; the lookup keys each handler by zone and NPC (or zone and csid), so the order
            // across zones changes nothing
            foreach (var (zone, npc, bit, csid) in WildcatNpcData)
            {
                accepted
                    .Npc(zone, npc).OnTrigger(q =>
                        !q.Quest.IsVarBitsSet("Prog", bit) ? QuestReply.ProgressEvent(csid) : null)
                    .OnEventFinish(zone, csid, q => q.Quest.SetVarBit("Prog", bit));
            }

            accepted
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Amutiyaal).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.Event(813);
                    }
                    else if (BastokWildcat.IsFull(questProgress, 20))
                    {
                        return QuestReply.ProgressEvent(815);
                    }
                    else
                    {
                        return QuestReply.Event(814);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 815, q =>
                {
                    q.Player.DelKeyItem(KeyItem.RedSentinelBadge);
                    q.Text.Special(Text.SouthernSanDoria.KEYITEM_LOST, KeyItem.RedSentinelBadge);

                    q.Quest.Complete();
                });
        }
    }
}
