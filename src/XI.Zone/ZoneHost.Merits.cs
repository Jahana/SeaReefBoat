using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // Merits (0x0BE), job points (0x0BF / 0x0C0) and the alter ego points request (0x0C1). CMeritPoints' upgrades are
    // CharData.Merits, CJobPoints' levels CharData.JobPointLevels. A merit edit rebuilds the skills, stats, abilities
    // and traits (BuildingCharTraitsTable, ZoneHost.Traits) and checks the worn gear (CheckValidEquipment) where C++ does.
    public partial class ZoneHost
    {
        private const ushort MsgBasicMeritIncrease = 380, MsgBasicMeritDecrease = 381, MsgBasicJobPointsIncrease = 720;
        private const ushort KeyItemCipherBracelet = 3361;

        private void RegisterMeritHandlers()
        {
            // GP_CLI_COMMAND_MERITS: Kind u8 @4 (2 ChangeMode, 3 EditMode), Param1 u8 @5 (0 Lower / EXP, 1 Raise / limit
            // points), Param2 u16 @6 (the merit id >> 1)
            Register(0x0BE, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .OneOf("Kind", p.Buffer[4], 2, 3)
                    .OneOf("Param1", p.Buffer[5], 0, 1),
                (s, c, p) =>
                {
                    byte kind = p.Buffer[4], param1 = p.Buffer[5];
                    ushort param2 = U16(p, 6);
                    if (kind == 2)
                    {
                        // the client stops a mode change in besieged or under a level restriction, but the server
                        // honours an injected one, as retail does
                        Exec("UPDATE char_exp SET mode = ? WHERE charid = ? LIMIT 1", BoundValue.Of(param1), BoundValue.Of(c.Id));
                        c.MeritMode = param1 != 0;
                        if (loader_.Stats != null) Push(s, SelfPackets.Merits(c, loader_.Stats));
                        Push(s, MonPage1(c));
                        Push(s, GameOkPackets.Monstrosity2(c));
                        return;
                    }
                    // allowed in a shared mog house too
                    if (c.MoghouseId == 0 || c.Merits == null) return;
                    ushort merit = (ushort)(param2 << 1);
                    if (!c.Merits.Table.Exists(merit)) return;
                    if (param1 == 0)
                    {
                        LowerMerit(c, merit);
                        Push(s, SelfPackets.BattleMessage(c, param2, c.Merits.Count(merit), MsgBasicMeritDecrease));
                    }
                    else
                    {
                        RaiseMerit(c, merit);
                        Push(s, SelfPackets.BattleMessage(c, param2, c.Merits.Count(merit), MsgBasicMeritIncrease));
                    }
                    var stats = loader_.Stats;
                    if (stats != null) Push(s, SelfPackets.Merits(c, stats));
                    Push(s, MonPage1(c));
                    Push(s, GameOkPackets.Monstrosity2(c));
                    Push(s, GameOkPackets.Merit(c, merit));

                    SaveCharExp(c, c.MJob);
                    SaveMeritPoints(c);

                    stats?.BuildSkillsTable(c);
                    loader_.CalculateStats(c);
                    stats?.CheckValidEquipment(c);
                    loader_.BuildAbilityTable(c);
                    BuildCharTraits(c);

                    CharStats.UpdateHealth(c);
                    c.Hp = c.MaxHp;
                    c.Mp = c.MaxMp;
                    Push(s, CharStatus(c));
                    if (stats != null) Push(s, SelfPackets.CliStatus(c, stats, UnityStatus(c)));
                    Push(s, SelfPackets.CliStatus2(c));
                    Push(s, AbilRecast(c));
                    Push(s, LoginPackets.CommandData(c));
                    SendExtendedJobPackets(s, c);
                    Push(s, CharSyncOf(c));
                });
            // GP_CLI_COMMAND_JOB_POINTS_SPEND: Index u16 @4 (the job point type); works in any mog house
            Register(0x0BF, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .MustEqual(c.MoghouseId != 0 ? 1 : 0, 1, "Character not in a mog house.")
                    .MustEqual(JobPointUtil.Exists(U16(p, 4)) ? 1 : 0, 1, "Job point does not exist."),
                (s, c, p) =>
                {
                    ushort type = U16(p, 4);
                    RaiseJobPoint(c, type);
                    Push(s, SelfPackets.JobPoints(c));
                    Push(s, JobPointUtil.Packet(c, type));
                    Push(s, SelfPackets.BattleMessage(c, type, JobPointUtil.Level(c, type), MsgBasicJobPointsIncrease));
                });
            // GP_CLI_COMMAND_JOB_POINTS_REQ: the job point details, once the char has unlocked them
            Register(0x0C0, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent),
                (s, c, p) =>
                {
                    if (c.HasKeyItem(SelfPackets.KeyItemJobBreaker)) Push(s, JobPointUtil.Packets(c));
                });
            // GP_CLI_COMMAND_ALTER_EGO_POINTS: CategoryIndex u16 @4 (8 HP .. 18 MagicSkills). The upgrade itself is a
            // TODO in the C++ too: the reply only re-sends the points.
            Register(0x0C1, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent | BlockedState.AbnormalStatus)
                    .OneOf("CategoryIndex", U16(p, 4), 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18)
                    .MustEqual(c.MoghouseId != 0 ? 1 : 0, 1, "Character is not in Mog House.")
                    .HasKeyItem(KeyItemCipherBracelet)
                    .MustEqual(c.MLvl < 99 ? 1 : 0, 0, "Main job must be Level 99"),
                (s, c, p) => Push(s, GameOkPackets.AlterEgoPoints(c)));
        }

        // CMeritPoints::RaiseMerit: the next upgrade's cost while the merit and its category have room, with the
        // spell or weapon skill the merit grants learned on the way
        private void RaiseMerit(CharData c, ushort merit)
        {
            var table = c.Merits.Table;
            table.ResolveGrants(db_);
            int index = table.IndexOf(merit);
            var cat = table.CategoryOf(merit);
            if (index < 0 || cat == null) return;
            var e = table.Entries[index];
            byte next = c.Merits.Next(index);
            if (c.MeritPoints < next || c.Merits.Counts[index] >= e.Upgrade || c.Merits.CountInSameCategory(merit) >= cat.MaxUpgrades) return;
            c.MeritPoints -= next;

            // charutils::addSpell: a spell that exists and is not known yet
            if (e.SpellId != 0 && SpellExists(e.SpellId) && !HasBit(c.SpellList, e.SpellId))
            {
                c.SpellList[e.SpellId / 8] |= (byte)(1 << e.SpellId % 8);
                Exec("INSERT IGNORE INTO char_spells VALUES (?, ?) LIMIT 1", BoundValue.Of(c.Id), BoundValue.Of(e.SpellId));
                PushTo(c, LoginPackets.MagicData(c));
            }
            if (e.WsUnlockId != 0 && e.WsUnlockId < 64 && !HasBit(c.LearnedWeaponskills, e.WsUnlockId))
            {
                c.LearnedWeaponskills[e.WsUnlockId / 8] |= (byte)(1 << e.WsUnlockId % 8);
                loader_.Stats?.BuildWeaponSkills(c);
                SaveLearnedAbilities(c);
                PushTo(c, LoginPackets.CommandData(c));
            }
            c.Merits.Counts[index]++;

            // Reset traits
            BuildCharTraits(c);
        }

        // CMeritPoints::LowerMerit: the points spent are not refunded; at no upgrades the spell and weapon skill go
        private void LowerMerit(CharData c, ushort merit)
        {
            var table = c.Merits.Table;
            table.ResolveGrants(db_);
            int index = table.IndexOf(merit);
            if (index < 0) return;
            var e = table.Entries[index];
            if (c.Merits.Counts[index] > 0) c.Merits.Counts[index]--;
            if (e.SpellId != 0 && c.Merits.Counts[index] == 0 && HasBit(c.SpellList, e.SpellId))
            {
                c.SpellList[e.SpellId / 8] &= (byte)~(1 << e.SpellId % 8);
                Exec("DELETE FROM char_spells WHERE charid = ? AND spellid = ? LIMIT 1", BoundValue.Of(c.Id), BoundValue.Of(e.SpellId));
                PushTo(c, LoginPackets.MagicData(c));

                // Reset traits
                BuildCharTraits(c);
            }
            if (e.WsUnlockId != 0 && c.Merits.Counts[index] == 0 && e.WsUnlockId < 64 && HasBit(c.LearnedWeaponskills, e.WsUnlockId))
            {
                c.LearnedWeaponskills[e.WsUnlockId / 8] &= (byte)~(1 << e.WsUnlockId % 8);
                loader_.Stats?.BuildWeaponSkills(c);
                SaveLearnedAbilities(c);
                PushTo(c, LoginPackets.CommandData(c));
            }
        }

        // CMeritPoints::SaveMeritPoints: every merit's upgrades, the ones without any deleted
        private void SaveMeritPoints(CharData c)
        {
            var table = c.Merits.Table;
            for (int i = 0; i < table.Entries.Count; i++)
            {
                ushort id = table.Entries[i].Id;
                byte count = c.Merits.Counts[i];
                if (count > 0)
                    Exec("INSERT INTO char_merit (charid, meritid, upgrades) VALUES(?, ?, ?) ON DUPLICATE KEY UPDATE upgrades = ?",
                         BoundValue.Of(c.Id), BoundValue.Of(id), BoundValue.Of(count), BoundValue.Of(count));
                else
                    Exec("DELETE FROM char_merit WHERE charid = ? AND meritid = ?", BoundValue.Of(c.Id), BoundValue.Of(id));
            }
        }

        // charutils::SaveLearnedAbilities: m_LearnedAbilities is 49 bytes, m_LearnedWeaponskills 8
        private void SaveLearnedAbilities(CharData c)
        {
            var abilities = new byte[49];
            Array.Copy(c.LearnedAbilities, abilities, Math.Min(c.LearnedAbilities.Length, abilities.Length));
            Exec("UPDATE chars SET abilities = ?, weaponskills = ? WHERE charid = ? LIMIT 1",
                 BoundValue.Of(abilities), BoundValue.Of(c.LearnedWeaponskills), BoundValue.Of(c.Id));
        }

        // CJobPoints::RaiseJobPoint: the next level's cost from the job's points (a job with no row has none to spend)
        private void RaiseJobPoint(CharData c, ushort type)
        {
            int job = type >> 5, j = type & 0x1F;
            byte cost = JobPointUtil.Cost(c.JobPointLevels[job, j]);
            var jp = c.JobPoints[job];
            if (cost == 0 || jp.Current < cost) return;
            c.JobPoints[job] = (jp.Capacity, (ushort)(jp.Current - cost), (ushort)(jp.Spent + cost));
            c.JobPointLevels[job, j]++;
            var args = new List<BoundValue>();
            for (int t = 0; t < 10; t++) args.Add(BoundValue.Of(c.JobPointLevels[job, t]));
            args.Add(BoundValue.Of(c.JobPoints[job].Current));
            args.Add(BoundValue.Of(c.JobPoints[job].Spent));
            args.Add(BoundValue.Of(c.Id));
            // the row's jobid: 0 for a job never loaded, so nothing is written
            args.Add(BoundValue.Of((byte)(c.JobPointRows[job] ? job : 0)));
            db_.Execute("UPDATE char_job_points SET jptype0 = ?, jptype1 = ?, jptype2 = ?, jptype3 = ?, jptype4 = ?, " +
                        "jptype5 = ?, jptype6 = ?, jptype7 = ?, jptype8 = ?, jptype9 = ?, job_points = ?, job_points_spent = ? " +
                        "WHERE charid = ? AND jobid = ?", args);
            if (loader_.RefreshGiftMods(c)) PushTo(c, LoginPackets.MagicData(c));
        }
    }

    // job_points.h's JOBPOINT_TYPE arithmetic and GP_SERV_COMMAND_JOB_POINTS (0x08D)
    public static class JobPointUtil
    {
        public const ushort BlueMagicPointBonus = 0x201; // JP_BLUE_MAGIC_POINT_BONUS

        // CJobPoints::IsJobPointExist: a job category from WAR to RUN and one of its ten types
        public static bool Exists(int type) => type >= 0x20 && (type >> 5) - 1 < 22 && (type & 0x1F) < 10;

        // JobPointCost: the next level's price, 0 once at 20
        public static byte Cost(int level) => (byte)((level + 1) % 21);

        public static byte Level(CharData c, int type) => Exists(type) ? c.JobPointLevels[type >> 5, type & 0x1F] : (byte)0;

        // CJobPoints::GetJobPointValue: the main job's own types at 99
        public static int Value(CharData c, int type) => Exists(type) && c.MLvl >= 99 && c.MJob == type >> 5 ? Level(c, type) : 0;

        private static void Entry(PacketWriter w, int slot, int id, byte level) =>
            w.U32(4 + 4 * slot, (uint)(id & 0x1F | (id >> 5) << 5 | Cost(level) << 16 | level << 26));

        // the full details: two jobs a packet (WAR alone in the first, then RUN with MON, then an empty one), each job's ten types
        // when its row was loaded
        public static List<BasicPacket> Packets(CharData c)
        {
            var list = new List<BasicPacket>();
            var w = new PacketWriter(0x08D, 260);
            int slot = 0;
            for (int job = 1; job < CharData.MaxJobs; job++)
            {
                if (c.JobPointRows[job])
                    for (int j = 0; j < 10 && slot < 64; j++) Entry(w, slot++, 0x20 * job + j, c.JobPointLevels[job, j]);
                if (job % 2 == 1)
                {
                    list.Add(w.Packet);
                    w = new PacketWriter(0x08D, 260);
                    slot = 0;
                }
            }
            list.Add(w.Packet);
            return list;
        }

        // one type in the first slot (its id 0 when the job's row was never loaded), still full size
        public static BasicPacket Packet(CharData c, int type)
        {
            var w = new PacketWriter(0x08D, 260);
            int job = type >> 5;
            Entry(w, 0, c.JobPointRows[job] ? type : 0, Level(c, type));
            return w.Packet;
        }
    }
}
