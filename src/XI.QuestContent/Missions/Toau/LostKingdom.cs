// Lost Kingdom (ToAU 13, Log ID 4, Mission ID 12), a line-for-line port of
// scripts/missions/toau/13_Lost_Kingdom.lua (quest rework C3 T1). Pyopyoroon !pos 22.112 0 24.682 53; Jazaraat's
// Headstone !pos -389 6 -570 79. Section 1 (the mission current): Aht Urhgan Whitegate's Naja plays 3071 (a plain event
// with the mercenary rank); Nashmau's Pyopyoroon plays 275 (plain). Caedarva Mire's Jazaraat's Headstone: at status 0
// with the Vial of Spectral Scent it plays cutscene 8, whose finish sets status 1; at status 1 while Jazaraat is not
// spawned it spawns him on the char's enmity and answers noAction (no message, as in captures); at status 2 it plays
// cutscene 9, whose finish takes the vial and completes the mission (the Ephramadian Gold Coin, The Dolphin Crest
// current). Jazaraat's death at status 1 sets status 2. Section 2 (the mission completed, whatever is current):
// the headstone gives the Ephramadian Gold Coin as a key item reply, else sends JAZARAATS_HEADSTONE. Kept as the Lua
// has it: the reward's coin and section 2's coin are two ways to hold it (a char who lost it can fetch it again).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class LostKingdom : MissionScript
    {
        public LostKingdom() : base(XI.Quests.MissionLog.ToAU, "LOST_KINGDOM") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/13_Lost_Kingdom";

        const int AreaId = MissionLog.Toau;
        const int MissionId = Mission.Toau.LostKingdom;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.EphramadianGoldCoin,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.TheDolphinCrest,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                    QuestReply.Event(3071, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0))
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.JazaraatsHeadstone).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0 && q.Player.HasKeyItem(KeyItem.VialOfSpectralScent))
                    {
                        return QuestReply.ProgressCutscene(8);
                    }
                    else if (missionStatus == 1 && !q.Player.MobIsSpawned(Mob.CaedarvaMire.JAZARAAT))
                    {
                        q.Player.SpawnMob(Mob.CaedarvaMire.JAZARAAT, 0, claim: false);
                        // There is no message returned here in captures
                        return QuestReply.NoAction();
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.ProgressCutscene(9);
                    }

                    return null;
                })
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.Jazaraat).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                    }
                })
                .OnEventFinish(Zone.CaedarvaMire, 8, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                })
                .OnEventFinish(Zone.CaedarvaMire, 9, q =>
                {
                    q.Player.DelKeyItem(KeyItem.VialOfSpectralScent);
                    q.Mission.Complete();
                })
                .Npc(Zone.Nashmau, Npc.Nashmau.Pyopyoroon).OnTrigger(QuestReply.Event(275));

            b.State(current: null, when: q => q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.JazaraatsHeadstone).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.EphramadianGoldCoin))
                    {
                        return QuestReply.KeyItem(KeyItem.EphramadianGoldCoin);
                    }

                    return QuestReply.MessageSpecial(Text.CaedarvaMire.JAZARAATS_HEADSTONE);
                });
        }
    }
}
