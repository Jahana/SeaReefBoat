// Port of world/conquest_system.{h,cpp}: the world server's side of conquest. Map servers report influence /
// mob kills / homepoints; this applies them to conquest_system, runs the weekly tally, and broadcasts the
// resulting influences / region controls to every map process.

using System;
using System.Collections.Generic;
using XI.Ipc;
using XI.Login;
using XI.Persistence;

namespace XI.World
{
    public sealed class ConquestSystem : IWorldSystem
    {
        private readonly IDatabase _db;
        private readonly Action<IIpcMessage> _broadcast; // IPCServer::broadcastMessage
        private readonly Action<string> _log;

        public ConquestSystem(IDatabase db, Action<IIpcMessage> broadcast, Action<string> log = null)
        {
            _db = db;
            _broadcast = broadcast;
            _log = log ?? (_ => { });
        }

        void IWorldSystem.HandleMessage(byte messageType, IppPayload message) => HandleMessage(messageType, message);

        public bool HandleMessage(byte messageType, IppPayload message)
        {
            switch ((ConquestMessage)messageType)
            {
                case ConquestMessage.M2W_GM_WeeklyUpdate:
                    UpdateWeekConquest();
                    return true;
                case ConquestMessage.M2W_GM_ConquestUpdate:
                    SendInfluencesMsg(true); // full update of influence points
                    return true;
                case ConquestMessage.M2W_AddInfluencePoints:
                {
                    // Applied to the DB only; time_server pushes influence out periodically.
                    var o = ConquestAddInfluencePoints.FromBytes(message.Payload);
                    if (o != null) UpdateInfluencePoints(o.Points, o.Nation, o.Region);
                    return true;
                }
                case ConquestMessage.M2W_AddMobKills:
                {
                    var o = ConquestAddCounter.FromBytes(message.Payload);
                    if (o != null) AddMobKills(o.Count, o.Region);
                    return true;
                }
                case ConquestMessage.M2W_AddPlayerHomepoints:
                {
                    var o = ConquestAddCounter.FromBytes(message.Payload);
                    if (o != null) AddPlayerHomepoints(o.Count, o.Region);
                    return true;
                }
                default:
                    _log($"Message: unknown conquest type message received: {messageType} from {message.Ipp}");
                    return false;
            }
        }

        private void SendTallyStartMsg() =>
            _broadcast(new ConquestEvent { TypeField = (byte)ConquestMessage.W2M_WeeklyUpdateStart });

        private void SendInfluencesMsg(bool shouldUpdateZones) =>
            _broadcast(new ConquestEvent
            {
                TypeField = (byte)ConquestMessage.W2M_BroadcastInfluencePoints,
                Payload = new ConquestInfluenceUpdate { ShouldUpdateZones = shouldUpdateZones, Influences = GetRegionalInfluences() }.ToBytes(),
            });

        private void SendRegionControlsMsg(ConquestMessage msgType) =>
            _broadcast(new ConquestEvent
            {
                TypeField = (byte)msgType,
                Payload = new ConquestRegionControlUpdate { RegionControls = GetRegionControls() }.ToBytes(),
            });

        // Returns true when the UPDATE failed (C++ returns !rset2); false when it succeeded or nothing applied.
        public bool UpdateInfluencePoints(int points, uint nation, byte region)
        {
            if (region == Region.Unknown) return false;
            if (nation > Nation.Windurst) return false;

            var rset = LoginDb.PreparedStmt(_db,
                "SELECT sandoria_influence, bastok_influence, windurst_influence FROM conquest_system WHERE region_id = ?", region);
            if (rset == null || rset.RowsCount() == 0 || !rset.Next()) return false;

            int[] influences =
            {
                rset.Get<int>("sandoria_influence"),
                rset.Get<int>("bastok_influence"),
                rset.Get<int>("windurst_influence"),
            };

            // 1st place gets 1x; 2nd/3rd get 3x if under half of 1st place's influence, else 2x.
            int firstPlace = Math.Max(influences[0], Math.Max(influences[1], influences[2]));
            if (influences[nation] < firstPlace / 2) points = unchecked(points * 3);
            else if (influences[nation] < firstPlace) points = unchecked(points * 2);

            // Scale the points; a non-zero gain is at least 1.
            if (points > 0) points = Math.Max(points / 10, 1);

            // Restricted by a factor of 100 because of packet lines in 0x05e_conquest.cpp
            const int influenceCap = int.MaxValue / 100;
            int room = influenceCap - influences[nation];

            if (points <= room)
            {
                influences[nation] += points;
            }
            else // Nation is full: gains come out of the other nations.
            {
                influences[nation] += room;
                int overflow = points - room;
                for (uint i = 0; i < 3; ++i)
                    if (i != nation) influences[i] -= Math.Min(influences[i], overflow);
            }

            var rset2 = LoginDb.PreparedStmt(_db,
                "UPDATE conquest_system SET sandoria_influence = ?, bastok_influence = ?, windurst_influence = ? WHERE region_id = ?",
                influences[0], influences[1], influences[2], region);
            return rset2 == null;
        }

        public void AddMobKills(int count, byte region)
        {
            if (LoginDb.PreparedStmt(_db, "UPDATE conquest_system SET mob_kills = mob_kills + ? WHERE region_id = ?", count, region) == null)
                _log("addMobKills: Failed to update mob_kills");
        }

        public void AddPlayerHomepoints(int count, byte region)
        {
            if (LoginDb.PreparedStmt(_db, "UPDATE conquest_system SET player_homepoints = player_homepoints + ? WHERE region_id = ?", count, region) == null)
                _log("addPlayerHomepoints: Failed to update player_homepoints");
        }

        // Called weekly: tallies region control, resets influence, and pushes the results to the map servers.
        public void UpdateWeekConquest()
        {
            SendTallyStartMsg();

            LoginDb.PreparedStmt(_db, "UPDATE conquest_system SET region_control_prev = region_control");

            var influences = GetRegionalInfluences();
            if (influences.Count == 0) _log("updateWeekConquest: no influence rows returned");

            for (int regionId = 0; regionId < influences.Count; ++regionId)
            {
                var influence = influences[regionId];

                int sandoria = influence.SandoriaInfluence;
                int bastok = influence.BastokInfluence;
                int windurst = influence.WindurstInfluence;
                int beastmen = ConquestData.CalculateBeastmenInfluence((byte)regionId, influence);

                byte control = Nation.Neutral;
                if (sandoria > bastok && sandoria > windurst && sandoria > beastmen) control = Nation.Sandoria;
                else if (bastok > sandoria && bastok > windurst && bastok > beastmen) control = Nation.Bastok;
                else if (windurst > sandoria && windurst > bastok && windurst > beastmen) control = Nation.Windurst;
                else if (beastmen > sandoria && beastmen > bastok && beastmen > windurst) control = Nation.Beastmen;

                LoginDb.PreparedStmt(_db, "UPDATE conquest_system SET region_control = ? WHERE region_id = ?", control, (byte)regionId);
            }

            if (LoginDb.PreparedStmt(_db, "UPDATE conquest_system SET sandoria_influence = 0, bastok_influence = 0, " +
                                          "windurst_influence = 0, mob_kills = 0, player_homepoints = 0") == null)
                _log("updateWeekConquest: Failed to reset influence");

            SendInfluencesMsg(false);
            SendRegionControlsMsg(ConquestMessage.W2M_WeeklyUpdateEnd);
        }

        // Called hourly: updates influence on every map server and requests a zone update.
        public void UpdateHourlyConquest() => SendInfluencesMsg(true);

        // Called every Vana'diel hour: updates influence on every map server, no zone update.
        public void UpdateVanaHourlyConquest() => SendInfluencesMsg(false);

        public List<Influence> GetRegionalInfluences()
        {
            var rset = LoginDb.PreparedStmt(_db,
                "SELECT sandoria_influence, bastok_influence, windurst_influence, mob_kills, player_homepoints FROM conquest_system ORDER BY region_id");

            var list = new List<Influence>();
            if (rset != null && rset.RowsCount() != 0)
            {
                while (rset.Next())
                {
                    list.Add(new Influence
                    {
                        SandoriaInfluence = rset.Get<int>("sandoria_influence"),
                        BastokInfluence = rset.Get<int>("bastok_influence"),
                        WindurstInfluence = rset.Get<int>("windurst_influence"),
                        MobKills = rset.Get<int>("mob_kills"),
                        PlayerHomepoints = rset.Get<int>("player_homepoints"),
                    });
                }
            }

            return list;
        }

        public List<RegionControl> GetRegionControls()
        {
            var rset = LoginDb.PreparedStmt(_db, "SELECT region_control, region_control_prev FROM conquest_system");

            var list = new List<RegionControl>();
            if (rset != null && rset.RowsCount() != 0)
            {
                while (rset.Next())
                    list.Add(new RegionControl { Current = rset.Get<byte>("region_control"), Prev = rset.Get<byte>("region_control_prev") });
            }

            return list;
        }
    }
}
