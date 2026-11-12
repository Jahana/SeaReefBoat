// Fork in the Road (WotG 50, Log ID 5, Mission ID 49), a line-for-line port of
// scripts/missions/wotg/50_Fork_in_the_Road.lua (quest rework C3 T8). blank_fork: Jugner Forest [S] !pos 46.333 0.189
// 309.025; La Vaule [S] !pos 294.982 3.497 -217.001; Southern San d'Oria [S] !pos 89.343 0.999 99.789; Beaucedine
// Glacier [S] !pos -69.917 -39.928 -192.761; Xarcabard [S] !pos -108.315 -25.605 -52.978; Throne Room [S] !pos 19.003 0
// -4.487; Walk of Echoes !pos -702.325 0.399 -481.664. One section (the mission current): in each of the seven zones,
// blank_fork without that zone's dawndrop key item plays the zone's progress event (zoneEventInfo: Jugner Forest [S] 7,
// La Vaule [S] 5, Southern San d'Oria [S] 176, Beaucedine Glacier [S] 29, Xarcabard [S] 42, Throne Room [S] 15, Walk of
// Echoes 10, each with its operands). Its finish counts the seven dawndrops held, gives the zone's one, and when six
// were held already says GATHERED_DAWNDROPS_LIGHT (with the Primal Glow) and RETRACED_ALL_JUNCTIONS (messageName, no
// entity) in the zone's own texts, takes each of the seven dawndrops held and then the Ronfaure Dawndrop, and
// completes the mission (the Primal Glow key item, Maiden of the Dusk current). The seven are taken in the order Lua's
// pairs walks zoneEventInfo, as the recorded traces show it (each delKeyItem sends the key item list): Beaucedine
// Glacier [S], Jugner Forest [S], Southern San d'Oria [S], Walk of Echoes, La Vaule [S], Xarcabard [S], Throne Room
// [S].
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class ForkInTheRoad : MissionScript
    {
        public ForkInTheRoad() : base(XI.Quests.MissionLog.WoTG, "FORK_IN_THE_ROAD") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/50_Fork_in_the_Road";

        // zoneEventInfo, in the order Lua's pairs walks it: the zone, the dawndrop it gives, its event and operands
        public static readonly (ushort Zone, int KeyItem, int[] Event)[] ZoneEventInfo =
        {
            (Zone.BeaucedineGlacierS, KeyItem.BeaucedineDawndrop, new[] { 29, 136, 23, 1756, 0, 0, 6357002, 0, 0 }),
            (Zone.JugnerForestS, KeyItem.JugnerDawndrop, new[] { 7, 82, 0, 0, 0, 0, 7340037, 0, 0 }),
            (Zone.SouthernSanDoriaS, KeyItem.SanDoriaDawndrop, new[] { 176, 80, 23, 1756, 2000, 0, 0, 0, 0 }),
            (Zone.WalkOfEchoes, KeyItem.WalkOfEchoesDawndrop, new[] { 10, 182, 23, 1756, 0, 0, 0, 1984, 1 }),
            (Zone.LaVauleS, KeyItem.LaVauleDawndrop, new[] { 5, 85, 23, 1756, 0, 85, 1, 0, 0 }),
            (Zone.XarcabardS, KeyItem.XarcabardDawndrop,
                new[] { 42, 137, 23, 2964, 2000, 66584575, 5941395, 4095, 0 }),
            (Zone.ThroneRoomS, KeyItem.ThroneRoomDawndrop, new[] { 15, 156, 23, 1756, 0, 156, 2, 0, 0 }),
        };

        static (ushort Zone, int KeyItem, int[] Event) InfoOf(ushort zone)
        {
            foreach (var info in ZoneEventInfo)
            {
                if (info.Zone == zone)
                {
                    return info;
                }
            }

            throw new System.InvalidOperationException($"no zoneEventInfo for zone {zone}");
        }

        // dawndropOnTrigger: the zone's event while its dawndrop is not held (player:getZoneID(), the handler's zone)
        static QuestReply DawndropOnTrigger(IQuestContext q)
        {
            var info = InfoOf(q.Zone);

            if (!q.Player.HasKeyItem(info.KeyItem))
            {
                return QuestReply.ProgressEvent(info.Event[0],
                    System.Array.ConvertAll(info.Event[1..], a => (QuestArg)a));
            }

            return null;
        }

        // dawndropOnEventFinish
        static void DawndropOnEventFinish(IQuestContext q)
        {
            int numDawndrops = 0;

            foreach (var info in ZoneEventInfo)
            {
                if (q.Player.HasKeyItem(info.KeyItem))
                {
                    numDawndrops = numDawndrops + 1;
                }
            }

            q.Player.GiveKeyItem(InfoOf(q.Zone).KeyItem);

            if (numDawndrops == 6)
            {
                q.Text.Special(new ZoneText(q.Zone, "GATHERED_DAWNDROPS_LIGHT"), KeyItem.PrimalGlow);
                q.Text.MessageName(new ZoneText(q.Zone, "RETRACED_ALL_JUNCTIONS"));

                foreach (var info in ZoneEventInfo)
                {
                    if (q.Player.HasKeyItem(info.KeyItem))
                    {
                        q.Player.DelKeyItem(info.KeyItem);
                    }
                }

                q.Player.DelKeyItem(KeyItem.RonfaureDawndrop);

                q.Mission.Complete();
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.PrimalGlow,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.MaidenOfTheDusk,
            });

            b.State(current: true)
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.blankfork).OnTrigger(DawndropOnTrigger)
                .OnEventFinish(Zone.JugnerForestS, 7, DawndropOnEventFinish)
                .Npc(Zone.LaVauleS, Npc.LaVauleS.blankfork).OnTrigger(DawndropOnTrigger)
                .OnEventFinish(Zone.LaVauleS, 5, DawndropOnEventFinish)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.blankfork).OnTrigger(DawndropOnTrigger)
                .OnEventFinish(Zone.SouthernSanDoriaS, 176, DawndropOnEventFinish)
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.blankfork).OnTrigger(DawndropOnTrigger)
                .OnEventFinish(Zone.BeaucedineGlacierS, 29, DawndropOnEventFinish)
                .Npc(Zone.XarcabardS, Npc.XarcabardS.blankfork).OnTrigger(DawndropOnTrigger)
                .OnEventFinish(Zone.XarcabardS, 42, DawndropOnEventFinish)
                .Npc(Zone.ThroneRoomS, Npc.ThroneRoomS.blankfork).OnTrigger(DawndropOnTrigger)
                .OnEventFinish(Zone.ThroneRoomS, 15, DawndropOnEventFinish)
                .Npc(Zone.WalkOfEchoes, Npc.WalkOfEchoes.blankfork).OnTrigger(DawndropOnTrigger)
                .OnEventFinish(Zone.WalkOfEchoes, 10, DawndropOnEventFinish);
        }
    }
}
