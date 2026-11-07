using System;
using System.Collections.Generic;
using XI.Ipc;
using XI.Map;
using XI.World;

namespace XI.Zone
{
    public enum WarpRequest : byte { None, HomePoint, Warp } // WarpRequest

    // requestedWarp and charutils::HomePoint: the death menu's Home Point (0x01A HomepointMenu) and the GM help desk's
    // self-unstuck (0x0F0 RESCUE) ask for it, and the zone tick (CZoneEntities::ZoneServer) sends the char to its home
    // point. 0x0F1 BUFFCANCEL is here too.
    public partial class ZoneHost
    {
        private const byte RegionTavnazia = 18;           // REGION_TYPE::TAVNAZIA, the last of the conquest regions
        private const byte ChatMessageSystem1 = 6;        // CHAT_MESSAGE_TYPE::MESSAGE_SYSTEM_1
        private const int MaxEffectId = 814;              // MAX_EFFECTID: 768 real + 46 custom

        private void RegisterWarpHandlers()
        {
            Register(0x0F0, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent | BlockedState.Crafting | BlockedState.Fishing |
                                                          BlockedState.Jailed | BlockedState.Engaged)
                                              .MustEqual(BitConverter.ToUInt32(p.Buffer, 4), 0, "State not 0"),
                     (s, c, p) => OnRescue(s, c));
            // DelStatusEffectsByIcon(BuffNo): every effect showing that icon that the char may cancel goes
            Register(0x0F1, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent).Range("BuffNo", BitConverter.ToUInt16(p.Buffer, 4), 1, MaxEffectId),
                     (s, c, p) => c.StatusEffects.DelStatusEffectsByIcon(BitConverter.ToUInt16(p.Buffer, 4)));
        }

        // GP_CLI_COMMAND_RESCUE: map.SELF_UNSTUCK_ENABLED, then the [GM]SelfUnstuck charvar as the cooldown
        private void OnRescue(MapSession s, CharData c)
        {
            var cfg = ChatCfg;
            if (!cfg.SelfUnstuckEnabled) return;
            if (CharVar(c, "[GM]SelfUnstuck") != 0)
            {
                log_($"{c.Name} requested self-unstuck but is still on cooldown.");
                Push(s, ChatStd(c, ChatMessageSystem1, CborRaw.Bytes("Self-unstuck is on cooldown.")));
                return;
            }
            log_($"{c.Name} requested self-unstuck, warping them to their Home Point.");
            SetCharVar(c, "[GM]SelfUnstuck", 1, (uint)(UnixNow() + (uint)cfg.SelfUnstuckCooldown));
            c.RequestedWarp = WarpRequest.HomePoint;
        }

        // the zone tick's requestedWarp branch: every zone is served here, so the home zone is always ready
        private void ProcessWarps()
        {
            foreach (var (c, s) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
            {
                if (c.RequestedWarp == WarpRequest.None || !s.InZone || s.Char != c) continue;
                zoneChanges_.Remove(s);
                // under --lazy a home point zone this process manages loads first
                IsZoneReady(c.HomeZone);
                s.PacketList.Clear();
                HomePoint(s, c, c.RequestedWarp == WarpRequest.HomePoint && c.Hp <= 0);
            }
        }

        // charutils::HomePoint, ending the death timer
        private bool HomePoint(MapSession s, CharData c, bool resetHpMp)
        {
            if (IsZoneAtPlayerCap(c.HomeZone, c.AccountGmLevel > 0))
            {
                s.PacketList.AddLast(SystemMessage(MsgCouldNotEnter));
                c.RequestedWarp = WarpRequest.None;
                return false;
            }

            if (resetHpMp)
            {
                // weakness and level sync go on a homepoint
                c.StatusEffects.DelStatusEffectSilent(EffectId.Weakness);
                c.StatusEffects.DelStatusEffectSilent(EffectId.LevelSync);
                c.DeathAtMs = 0; // SetDeathTime(min)
                c.Hp = c.MaxHp;
                c.Mp = c.MaxMp;
                // homepointing increases beastmen influence
                byte region = ZoneRegions.Of(c.Zone);
                if (region <= RegionTavnazia && c.MLvl >= (byte)ChatCfg.MinimumLevelConquestInfluenceLoss)
                    SendIpc(new ConquestEvent
                    {
                        TypeField = (byte)ConquestMessage.M2W_AddPlayerHomepoints,
                        Payload = new ConquestAddCounter { Count = 1, Region = region }.ToBytes(),
                    });
            }

            c.DeathAtMs = 0;
            c.Boundary = 0;
            (c.X, c.Y, c.Z, c.Rotation) = (c.HomeX, c.HomeY, c.HomeZ, c.HomeRot);
            c.Animation = AnimationNone;
            c.UpdateMask |= CharStats.UpdateHp;
            s.PacketList.Clear();
            // SendToZone un-requests the warp, so a warp that gets here cannot loop; turned away, the warp is tried again
            return SendToZone(s, c, c.HomeZone);
        }
    }
}
