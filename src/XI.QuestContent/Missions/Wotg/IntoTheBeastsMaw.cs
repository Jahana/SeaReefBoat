// Into the Beast's Maw (WotG 33, Log ID 5, Mission ID 32), a line-for-line port of
// scripts/missions/wotg/33_Into_the_Beasts_Maw.lua (quest rework C3 T7). Rally Point: Red !pos -106.071 -25.5 -52.841
// 137; Peculiar Glint !pos 179.439 -24.056 100.032 138. One section (the mission current). In Xarcabard [S] Rally
// Point: Red shows JOIN_ALLIED_FORCE (messageSpecial). In Castle Zvahl Baileys [S]: the Ornate Block, when the char
// lacks the Distress Signal Flare at Status 4, plays the progress event 11 (138) while the Timer var is not past the
// Vana'diel day (its finish gives the flare on option 1) and after it shows CANNOT_FIND_FLARE (messageName, no entity,
// the flare as its parameter) and plays nothing. The Peculiar Glint at Status 1 plays the progress event 2 (53, 23,
// 1756, 0, 0, 16384001, 0, 0), whose finish sets Status 2 and sets the char down at 183.405 -24.041 100.041 rot 126 in
// the same zone. The zone-in plays the plain event 1 at Status 0 (its finish sets Status 1), 14 at Status 2 (its update
// with option 5 sends the update (138, 23, 1756, 0, 0, 0, 0, 0); its finish sets Status 3 and the same set-down), 15 at
// Status 3 (its finish gives the flare and sets Status 4) and 3 at Status 5 (its finish completes the mission, The
// Hunter Ensnared current). Ghoyu's Reverie's event 10000 sets Status 5 and sets the char down at 179.987 -24.046
// 94.225 rot 194 in Castle Zvahl Baileys [S]. Kept as the Lua has it: Timer is read but never set in this mission. No
// flow statement starts 10000 (the harness has no instance run); fake checks pin that finish and its set-down.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class IntoTheBeastsMaw : MissionScript
    {
        public IntoTheBeastsMaw() : base(XI.Quests.MissionLog.WoTG, "INTO_THE_BEASTS_MAW") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/33_Into_the_Beasts_Maw";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.TheHunterEnsnared,
            });

            b.State(current: true)
                .Npc(Zone.XarcabardS, Npc.XarcabardS.RallyPointRed)
                .OnTrigger(QuestReply.MessageSpecial(Text.XarcabardS.JOIN_ALLIED_FORCE))
                .Npc(Zone.CastleZvahlBaileysS, Npc.CastleZvahlBaileysS.OrnateBlock).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.DistressSignalFlare) && q.Mission.Var("Status") == 4)
                    {
                        if (q.Mission.Var("Timer") <= q.Player.VanadielUniqueDay)
                        {
                            return QuestReply.ProgressEvent(11, 138);
                        }
                        else
                        {
                            q.Text.MessageName(Text.CastleZvahlBaileysS.CANNOT_FIND_FLARE, KeyItem.DistressSignalFlare);

                            return QuestReply.NoAction();
                        }
                    }

                    return null;
                })
                .Npc(Zone.CastleZvahlBaileysS, Npc.CastleZvahlBaileysS.PeculiarGlint).OnTrigger(q =>
                {
                    // the Lua's TODO: the instance entry wants the Distress Signal Flare (entry event 6, options 0, 32;
                    // the char sent to Ghoyu's Reverie at 467.375 -0.173 119.999 rot 2)
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(2, 53, 23, 1756, 0, 0, 16384001, 0, 0);
                    }

                    return null;
                })
                .OnZoneIn(Zone.CastleZvahlBaileysS, q =>
                {
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 0)
                    {
                        return QuestReply.Event(1);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.Event(14);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.Event(15);
                    }
                    else if (missionStatus == 5)
                    {
                        return QuestReply.Event(3);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.CastleZvahlBaileysS, 14, q =>
                {
                    if (q.Option == 5)
                    {
                        q.Player.UpdateEvent(138, 23, 1756, 0, 0, 0, 0, 0);
                    }
                })
                .OnEventFinish(Zone.CastleZvahlBaileysS, 1, q =>
                {
                    q.Mission.SetVar("Status", 1);
                })
                .OnEventFinish(Zone.CastleZvahlBaileysS, 2, q =>
                {
                    q.Mission.SetVar("Status", 2);
                    q.Player.SetPos(183.405, -24.041, 100.041, 126, Zone.CastleZvahlBaileysS);
                })
                .OnEventFinish(Zone.CastleZvahlBaileysS, 3, q =>
                {
                    q.Mission.Complete();
                })
                .OnEventFinish(Zone.CastleZvahlBaileysS, 11, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.DistressSignalFlare);
                    }
                })
                .OnEventFinish(Zone.CastleZvahlBaileysS, 14, q =>
                {
                    q.Mission.SetVar("Status", 3);
                    q.Player.SetPos(183.405, -24.041, 100.041, 126, Zone.CastleZvahlBaileysS);
                })
                .OnEventFinish(Zone.CastleZvahlBaileysS, 15, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.DistressSignalFlare);
                    q.Mission.SetVar("Status", 4);
                })
                .OnEventFinish(Zone.GhoyusReverie, 10000, q =>
                {
                    // the Lua's TODO: the script assumes the instance's clear sends 10000
                    q.Mission.SetVar("Status", 5);
                    q.Player.SetPos(179.987, -24.046, 94.225, 194, Zone.CastleZvahlBaileysS);
                });
        }
    }
}
