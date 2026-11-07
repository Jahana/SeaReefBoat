using System;
using System.Collections.Generic;
using XI.Persistence;

namespace XI.Zone
{
    // CWeaponSkill: a weapon_skills row
    public sealed class WeaponSkill
    {
        public ushort Id;
        public string Name;
        public readonly byte[] Jobs = new byte[CharData.MaxJobs]; // m_Job: [0] (NON) and the jobs past the blob stay 0
        public byte Type, Element, AnimationId, Range, Aoe, Radius, PrimarySc, SecondarySc, TertiarySc, UnlockId;
        public ushort SkillLevel;
        public uint AnimationTimeMs;
        public bool MainOnly;

        public bool IsAoE => Aoe == 2;
        public bool IsSingle => Aoe == 1;
        public byte GetJob(int job) => job >= 0 && job < Jobs.Length ? Jobs[job] : (byte)0;
    }

    // battleutils::LoadWeaponSkillsList: g_PWeaponSkillList by id and g_PWeaponSkillsList per skill type, each in
    // skill level order
    public sealed class WeaponSkillTable
    {
        public const int MaxWeaponSkillId = 256; // MAX_WEAPONSKILL_ID

        private readonly WeaponSkill[] byId_ = new WeaponSkill[MaxWeaponSkillId];
        private readonly List<WeaponSkill>[] byType_ = new List<WeaponSkill>[Skill.Max];

        public WeaponSkillTable()
        {
            for (int i = 0; i < byType_.Length; i++) byType_[i] = new List<WeaponSkill>();
        }

        public void Add(WeaponSkill ws)
        {
            byId_[ws.Id] = ws;
            if (ws.Type < byType_.Length) byType_[ws.Type].Add(ws);
        }

        // battleutils::GetWeaponSkill: null past MAX_WEAPONSKILL_ID or for an id with no row
        public WeaponSkill Get(int id) => id >= 0 && id < MaxWeaponSkillId ? byId_[id] : null;

        // battleutils::GetWeaponSkills: the None list for a skill past MAX_SKILLTYPE
        public IReadOnlyList<WeaponSkill> ForSkill(int skill) => skill >= 0 && skill < byType_.Length ? byType_[skill] : byType_[Skill.None];

        public static WeaponSkillTable Load(IDatabase db)
        {
            var table = new WeaponSkillTable();
            var r = db.Execute("SELECT weaponskillid, name, jobs, type, skilllevel, element, animation, animationTime, `range`, aoe, radius, " +
                               "primary_sc, secondary_sc, tertiary_sc, main_only, unlock_id FROM weapon_skills WHERE weaponskillid < ? " +
                               "ORDER BY type, skilllevel ASC", new List<BoundValue> { BoundValue.Of(MaxWeaponSkillId) });
            while (r != null && r.Next())
            {
                var ws = new WeaponSkill
                {
                    Id = r.Get<ushort>("weaponskillid"), Name = r.Get<string>("name"), Type = r.Get<byte>("type"),
                    SkillLevel = r.Get<ushort>("skilllevel"), Element = r.Get<byte>("element"), AnimationId = r.Get<byte>("animation"),
                    AnimationTimeMs = r.Get<uint>("animationTime"), Range = r.Get<byte>("range"), Aoe = r.Get<byte>("aoe"),
                    Radius = r.Get<byte>("radius"), PrimarySc = (byte)r.Get<sbyte>("primary_sc"), SecondarySc = (byte)r.Get<sbyte>("secondary_sc"),
                    TertiarySc = (byte)r.Get<sbyte>("tertiary_sc"), MainOnly = r.Get<byte>("main_only") != 0, UnlockId = (byte)r.Get<sbyte>("unlock_id"),
                };
                // 22 jobs in the blob, from WAR at [1]
                var blob = r.IsNull("jobs") ? null : r.GetBlobBytes("jobs");
                if (blob != null) Array.Copy(blob, 0, ws.Jobs, 1, Math.Min(Math.Min(blob.Length, 22), ws.Jobs.Length - 1));
                table.Add(ws);
            }
            return table;
        }
    }
}
