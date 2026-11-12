// Ancient Flames Beckon, the shared hooks of the three Spire battlefields (Dem, Holla, Mea), a line-for-line port of
// scripts/battlefields/Spire_of_{Dem,Holla,Mea}/ancient_flames_beckon.lua (quest rework C3), whose three copies differ
// only in the crag. Each is a plain Battlefield (isMission) of 30 minutes for six, trusts allowed, no experience lost,
// capped at xi.settings.main.MAX_LEVEL, granting 1500 experience once per JST day. It overrides three methods:
// entryRequirement (Below the Arks current, or The Mothercrystals current without this crag's Light key item; a helper
// also with The Mothercrystals completed or the Light), checkSkipCutscene (The Mothercrystals completed or the Light)
// and onBattlefieldWin (starts event 32001; sets newPromy when the mission is Below the Arks or The Mothercrystals and
// the char lacks the Light of the crag at LIGHT_OF_HOLLA + (zone - 17) / 2, and sets battlefieldWin to the id).
// Kept as the Lua has it: the key item of onBattlefieldWin is read by the zone, that of the others by the crag's name.
using XI.QuestContent.Missions.Cop;
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields
{
    public abstract class AncientFlamesBeckon : BattlefieldScript
    {
        protected AncientFlamesBeckon(string name) : base(name) { }

        // the crag (PromyvionHelpers.Holla, Dem or Mea) and its Light key item
        protected abstract int Crag { get; }
        protected abstract int Light { get; }

        public override bool EntryRequirement(IBattlefieldContext q, bool isRegistrant)
        {
            var player = q.Player;
            int promathiaMission = player.CurrentMission(MissionLog.Cop);
            bool currentRequirements = promathiaMission == Mission.Cop.BelowTheArks ||
                (promathiaMission == Mission.Cop.TheMothercrystals && !player.HasKeyItem(Light));
            bool nonRegistrantReqs = player.HasCompletedMission(MissionLog.Cop, Mission.Cop.TheMothercrystals) ||
                player.HasKeyItem(Light) ||
                currentRequirements;

            return (!isRegistrant && nonRegistrantReqs) || currentRequirements;
        }

        public override bool CheckSkipCutscene(IBattlefieldContext q)
        {
            return q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.TheMothercrystals) ||
                q.Player.HasKeyItem(Light);
        }

        public override void OnBattlefieldWin(IBattlefieldContext q)
        {
            var player = q.Player;
            var record = q.Battlefield.Record();

            int arg8 = PromyvionHelpers.NumCompleted(player, Crag) + 1;
            int copMission = player.CurrentMission(MissionLog.Cop);
            int promyvionId = (player.ZoneId - 17) / 2;

            if ((copMission == Mission.Cop.BelowTheArks || copMission == Mission.Cop.TheMothercrystals) &&
                !player.HasKeyItem(KeyItem.LightOfHolla + promyvionId))
            {
                player.SetLocalVar("newPromy", 1);
            }

            int repeatClear = CheckSkipCutscene(q) ? 1 : 0;

            player.SetLocalVar("battlefieldWin", q.Battlefield.Id);
            player.StartEvent(32001, q.Battlefield.Area, record.ClearTime, record.PartySize,
                q.Battlefield.TimeInside, player.ZoneId, Data.Index, repeatClear, arg8);
        }
    }
}
