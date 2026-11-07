// What the three Mog House moogle quests (Give a Moogle a Break, The Moogle's Picnic!, Moogles in the Wild) and their
// era modules share (quest rework B3b Other Areas group 9): the Mog House zones with each zone's Moogle, and the two
// safes their rewards grow.
namespace XI.QuestContent.OtherAreas
{
    internal static class MogHouse
    {
        // xi.moghouse.moghouseZones (scripts/globals/moghouse.lua), in its order, each with its zone's Moogle (every
        // one is named Moogle; each zone's own constant keeps the registration's name check)
        internal static readonly (ushort Zone, string Moogle)[] Moogles =
        {
            (Zone.AlZahbi, Npc.AlZahbi.Moogle),                         // 48 (moghouse.lua's comment says 49)
            (Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Moogle),   // 50
            (Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Moogle),     // 80
            (Zone.BastokMarketsS, Npc.BastokMarketsS.Moogle),           // 87
            (Zone.WindurstWatersS, Npc.WindurstWatersS.Moogle),         // 94
            (Zone.SouthernSanDoria, Npc.SouthernSanDoria.Moogle),       // 230
            (Zone.NorthernSanDoria, Npc.NorthernSanDoria.Moogle),       // 231
            (Zone.PortSanDoria, Npc.PortSanDoria.Moogle),               // 232
            (Zone.BastokMines, Npc.BastokMines.Moogle),                 // 234
            (Zone.BastokMarkets, Npc.BastokMarkets.Moogle),             // 235
            (Zone.PortBastok, Npc.PortBastok.Moogle),                   // 236
            (Zone.WindurstWaters, Npc.WindurstWaters.Moogle),           // 238
            (Zone.WindurstWalls, Npc.WindurstWalls.Moogle),             // 239
            (Zone.PortWindurst, Npc.PortWindurst.Moogle),               // 240
            (Zone.WindurstWoods, Npc.WindurstWoods.Moogle),             // 241
            (Zone.RuludeGardens, Npc.RuludeGardens.Moogle),             // 243
            (Zone.UpperJeuno, Npc.UpperJeuno.Moogle),                   // 244
            (Zone.LowerJeuno, Npc.LowerJeuno.Moogle),                   // 245
            (Zone.PortJeuno, Npc.PortJeuno.Moogle),                     // 246
            (Zone.WesternAdoulin, Npc.WesternAdoulin.Moogle),           // 256
            (Zone.EasternAdoulin, Npc.EasternAdoulin.Moogle),           // 257
        };

        // xi.inv (scripts/enum/inventory_location.lua): no generated constant
        internal const int MogSafe = 1;
        internal const int MogSafe2 = 9;
    }
}
