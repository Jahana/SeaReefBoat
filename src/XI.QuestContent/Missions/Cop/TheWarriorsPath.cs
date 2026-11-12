// The Warrior's Path (Promathia 7-5, Log ID 6, Mission ID 748), a line-for-line port of
// scripts/missions/cop/7_5_The_Warriors_Path.lua (quest rework C3, C4). The mission current. Sealion's Den: _0w0 plays
// 32 at Status 0 (update with option 0: updateEvent(32, 0, 0, 0, 0, 0, 0, 0); finish: Status 1); Sueleen plays the
// event 28; the 32001 finish with The Warrior's Path won (battlefield.warriors_path sets battlefieldWin) at Status 1
// sets Status 2 and the char down in Sealion's Den, whose zone-in at Status 2 plays 34 (Status 3, set down in
// Al'Taieu). Al'Taieu's zone-in at Status 3 plays 1; its finish takes the drained amulet (AMULET_SHATTERED), takes the
// race's light and gives the Light of Al'Taieu (LIGHT_STOLEN; races 1-2 Vahzl, 3-4 Mea, 5-6 Holla, 7 Dem), or for a
// Galka keeps the Light of Al'Taieu (OBTAIN_BUT_STOLEN), completes the mission, sets Option 1 and the char var
// SagheeraInteractions 7. Once complete: Sueleen plays the event 12 (finish with option 1: the teleport to the Sea)
// and Sealion's Den's zone-in plays 18 while Option is 1 (finish: Option 0). Garden of Antiquity is next, with the
// Seeker of the Light title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class TheWarriorsPath : MissionScript
    {
        public TheWarriorsPath() : base(XI.Quests.MissionLog.CoP, "THE_WARRIORS_PATH") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/7_5_The_Warriors_Path";

        // xi.teleport.id.SEA
        const int TeleportSea = 56;

        // stolenKeyTable
        static readonly int[] StolenKeyTable =
        {
            KeyItem.LightOfVahzl,
            KeyItem.LightOfMea,
            KeyItem.LightOfHolla,
            KeyItem.LightOfDem,
            KeyItem.LightOfAltaieu,
        };

        // getStolenKeyItem: math.ceil(race / 2) up to xi.race.MITHRA (7), else 5
        static int GetStolenKeyItem(IQuestContext q)
        {
            int raceId = q.Player.Race <= 7 ? (q.Player.Race + 1) / 2 : 5;
            return StolenKeyTable[raceId - 1];
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.SeekerOfTheLight,
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.GardenOfAntiquity,
            });

            b.State(current: true)
                .Npc(Zone.SealionsDen, Npc.SealionsDen._0w0).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.ProgressEvent(32) : null)
                .Npc(Zone.SealionsDen, Npc.SealionsDen.Sueleen).OnTrigger(QuestReply.Event(28))
                .OnZoneIn(Zone.SealionsDen, q => q.Mission.Var("Status") == 2 ? QuestReply.Event(34) : null)
                .OnEventUpdate(Zone.SealionsDen, 32, q =>
                {
                    if (q.Option == 0) q.Player.UpdateEvent(32, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.SealionsDen, 32, q => q.Mission.SetVar("Status", 1))
                .OnEventFinish(Zone.SealionsDen, 34, q =>
                {
                    q.Mission.SetVar("Status", 3);
                    q.Player.SetPos(-422.100, 0, -532.092, 220, Zone.Altaieu);
                })
                .OnEventFinish(Zone.SealionsDen, 32001, q =>
                {
                    if (q.Mission.Var("Status") == 1 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.WarriorsPath)
                    {
                        q.Mission.SetVar("Status", 2);
                        q.Player.SetPos(612.057, 132.664, 776.920, 188, Zone.SealionsDen);
                    }
                })
                .OnZoneIn(Zone.Altaieu, q => q.Mission.Var("Status") == 3 ? QuestReply.Event(1) : null)
                .OnEventFinish(Zone.Altaieu, 1, q =>
                {
                    int stolenLight = GetStolenKeyItem(q);
                    q.Player.DelKeyItem(KeyItem.MysteriousAmuletDrained);
                    q.Text.Special(Text.Altaieu.AMULET_SHATTERED, KeyItem.MysteriousAmulet);

                    if (stolenLight != KeyItem.LightOfAltaieu)
                    {
                        q.Player.DelKeyItem(stolenLight);
                        q.Text.Special(Text.Altaieu.LIGHT_STOLEN, stolenLight);
                        q.Player.GiveKeyItem(KeyItem.LightOfAltaieu);
                    }
                    else
                    {
                        q.Text.Special(Text.Altaieu.OBTAIN_BUT_STOLEN, stolenLight);
                    }

                    q.Mission.Complete();
                    q.Mission.SetVar("Option", 1);
                    // Setup var for initial interactions with Sagheera.
                    // This is a bitfield that has its bits removed as the player completes the necessary interactions.
                    q.Player.SetCharVar("SagheeraInteractions", 7);
                });

            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.TheWarriorsPath))
                .Npc(Zone.SealionsDen, Npc.SealionsDen.Sueleen).OnTrigger(QuestReply.Event(12))
                .OnZoneIn(Zone.SealionsDen, q => q.Mission.Var("Option") == 1 ? QuestReply.Event(18) : null)
                .OnEventFinish(Zone.SealionsDen, 12, q =>
                {
                    if (q.Option == 1) q.Player.TeleportTo(TeleportSea);
                })
                .OnEventFinish(Zone.SealionsDen, 18, q => q.Mission.SetVar("Option", 0));
        }
    }
}
