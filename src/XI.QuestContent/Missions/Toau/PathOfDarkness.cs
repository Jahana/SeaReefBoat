// Path of Darkness (ToAU 42, Log ID 4, Mission ID 41), a line-for-line port of
// scripts/missions/toau/42_Path_of_Darkness.lua (quest rework C3 T4). Rodin-Comidin !pos 17.205 -5.999 51.161 50,
// blank_lamp !pos 206.55 -1.5 20.05 72, _1e1 (door) !pos 23 -6 -63 50. One section (the mission current). Aht Urhgan
// Whitegate: Naja Salaheem plays the plain event 3148; Rodin-Comidin plays the progress event 3142 without the Nyzul
// Isle Route key item, 3141 with it (text table 0 both), 3142's finish giving the key item. Trigger area 3 at status
// above 0 plays the progress event 3143 (operands 0, 1 and the blockedDialog mission local var seventh); its update
// with option 0 answers 0, 1; its finish sets blockedDialog to 1 and sets the char down at 23.978 -6 -64.624 (rotation
// 63). Alzadaal Undersea Ruins: blank_lamp at status 0 plays the progress event 6, whose finish with option 0 sets
// status 1; the zone-in at status 2 plays 7, whose finish completes the mission (Fangs of the Lion current, the Najas
// Comrade in Arms title). The finish of 116 (the Runic Seal's event, a workaround in the Lua) with option 1 and the
// INSTANCE_ID local var 7700 runs xi.instance.onEventFinish, which stays Lua (the Nyzul Isle instance scripts); the
// bridge passes nil for the npc, as nothing in it reads the npc. The Lua passes a ninth updateEvent operand, always 0;
// the port drops it (the engine reads eight, so nothing changes).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class PathOfDarkness : MissionScript
    {
        public PathOfDarkness() : base(XI.Quests.MissionLog.ToAU, "PATH_OF_DARKNESS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/42_Path_of_Darkness";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.NajasComradeInArms,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.FangsOfTheLion,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3148, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.RodinComidin).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.NyzulIsleRoute))
                    {
                        return QuestReply.ProgressEvent(3142).TextTable(0);
                    }

                    return QuestReply.ProgressEvent(3141).TextTable(0);
                })
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 3, q =>
                {
                    if (q.Player.MissionStatus(AreaId) > 0)
                    {
                        int blockedDialog = q.Mission.LocalVar("blockedDialog");

                        return QuestReply.ProgressEvent(3143, 0, 1, 0, 0, 0, 0, blockedDialog, 0, 0);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3143, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.UpdateEvent(0, 1, 0, 0, 0, 0, 0, 0);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3142, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.NyzulIsleRoute);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3143, q =>
                {
                    q.Mission.SetLocalVar("blockedDialog", 1);
                    q.Player.SetPos(23.978, -6, -64.624, 63);
                })
                .Npc(Zone.AlzadaalUnderseaRuins, Npc.AlzadaalUnderseaRuins.blanklamp).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.ProgressEvent(6);
                    }

                    return null;
                })
                .OnZoneIn(Zone.AlzadaalUnderseaRuins, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2)
                    {
                        return QuestReply.Event(7);
                    }

                    return null;
                })
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 6, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.SetMissionStatus(AreaId, 1);
                    }
                })
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 7, q =>
                {
                    q.Mission.Complete();
                })
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 116, q =>
                {
                    if (q.Option == 1 && q.Player.LocalVar("INSTANCE_ID") == 7700)
                    {
                        q.Player.InstanceOnEventFinish(116, q.Option);
                    }
                });
        }
    }
}
