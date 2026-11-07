using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;
using LuaFunction = XI.Script.LuaFunction;

namespace XI.Zone
{
    // CSpell: a spell_list row, and what one cast carries on its copy (the message, modifier, critical, the targets
    // and the flag), and a blue spell's knockback (CBlueSpell::getKnockback). Not ported: the rest of CBlueSpell's
    // extra data (GetBlueSpell in ZoneHost.ExtendedJob.cs has the set points and the skillchain properties) and the
    // requirements.
    public sealed class Spell
    {
        // SPELLGROUP
        public const byte GroupNone = 0, GroupSong = 1, GroupBlack = 2, GroupBlue = 3, GroupNinjutsu = 4, GroupSummoning = 5,
                          GroupWhite = 6, GroupGeomancy = 7, GroupTrust = 8;
        // SPELLAOE
        public const byte AoeNone = 0, AoeRadial = 1, AoeConal = 2, AoeRadialMani = 3, AoeRadialAcce = 4, AoePianissimo = 5,
                          AoeDiffusion = 6;
        // SPELLFLAG
        public const byte FlagWipeShadows = 0x02, FlagIgnoreShadows = 0x04, FlagNoStartMsg = 0x08, FlagNoFinishMsg = 0x10;
        // SPELLFAMILY
        public const ushort FamilyBio = 64, FamilyEleDot = 65;
        // xi::SkillType
        public const byte SkillDivine = 32, SkillHealing = 33, SkillEnhancing = 34, SkillEnfeebling = 35, SkillElemental = 36,
                          SkillDark = 37, SkillSummoning = 38, SkillNinjutsu = 39, SkillSinging = 40, SkillString = 41,
                          SkillWind = 42, SkillBlue = 43, SkillGeomancy = 44, SkillHandbell = 45;
        // the SpellID values the predicates name
        public const ushort Cura = 93, RaiseIII = 140, Erase = 143, Meteor = 218, Comet = 219, MeteorII = 244, Stun = 252,
                            Curse = 257, Death = 367, CuraII = 474, CuraIII = 475, Embrava = 478, Arise = 494, Kaustra = 502,
                            Impact = 503, Pollen = 549, WildCarrot = 578, HealingBreeze = 581, MagicFruit = 593;

        public ushort Id;
        public string Name = "";
        public readonly byte[] Jobs = new byte[CharData.MaxJobs]; // the level per job, by job id
        public byte Group, Skill, Aoe, Element, Requirements, StatusEffectTier;
        public byte Knockback; // CBlueSpell::knockback_ (blue_spell_list; None for any other spell)
        public ushort Family, ValidTargets, ZoneMisc, Animation, MpCost, Base, Message, MagicBurstMessage;
        public long CastTimeMs, RecastTimeMs, AnimationTimeMs;
        public float Multiplier, Range, Radius;
        public int CE, VE;
        public string ContentTag = "";
        public ushort? StatusEffect;

        // what one cast carries (the state's copy)
        public uint Modifier;
        public bool Critical;
        public ushort TotalTargets; // m_totalTargets{}: 0 on the template, the cast's count on its copy
        public uint PrimaryTargetId;
        public byte Flag;

        // CSpell::getJob: a job without the spell (CANNOT_USE_SPELL, 0) reads 255
        public byte Job(int job) => job >= 0 && job < Jobs.Length && Jobs[job] != 0 ? Jobs[job] : (byte)255;

        public bool CanTargetEnemy => (ValidTargets & ZoneHost.SpellTargetEnemy) != 0 && (ValidTargets & ZoneHost.SpellTargetSelf) == 0;
        public bool IsBuff => (ValidTargets & ZoneHost.SpellTargetSelf) != 0 && (ValidTargets & ZoneHost.SpellTargetEnemy) == 0;
        public bool IsHeal => (ValidTargets & ZoneHost.SpellTargetSelf) != 0 && Skill == SkillHealing ||
                              Id is Pollen or WildCarrot or HealingBreeze or MagicFruit;
        public bool IsNa => Id is >= 14 and <= 20 or Erase;
        public bool IsSevere => Id is Death or Impact or Meteor or MeteorII or Comet;
        public bool IsDebuff => (ValidTargets & ZoneHost.SpellTargetEnemy) != 0 && Skill == SkillEnfeebling ||
                                Family is FamilyEleDot or FamilyBio || Id is Stun or Curse;
        public bool IsRaise => Id is >= 12 and <= 13 or RaiseIII or Arise;
        public bool IsCure => Id is >= 1 and <= 11 or Cura or CuraII or CuraIII;
        public bool CanHitShadow => Id != MeteorII && CanTargetEnemy;
        public bool HasMpCost => Group != GroupSong && Group != GroupNinjutsu && Group != GroupTrust;
        // damage or drain hp: MagicDamage, MagicDrainsHP, MagicBurstDamage, MagicBurstDrainsHP
        public bool DealsDamage => Message is 2 or 227 or 252 or 274;
        // not MagicNoEffect, MagicResistedTarget, TargetNoEffect, MagicResisted, MagicCompleteResist, MagicFail
        public bool TookEffect => Message is not (75 or 284 or 283 or 85 or 655 or 114);

        // CSpell::getFourCC
        public FourCC FourCCOf(bool interrupt) => Group switch
        {
            GroupWhite => interrupt ? FourCC.WhiteMagicInterrupt : FourCC.WhiteMagicCast,
            GroupBlack => interrupt ? FourCC.BlackMagicInterrupt : FourCC.BlackMagicCast,
            GroupBlue => interrupt ? FourCC.BlueMagicInterrupt : FourCC.BlueMagicCast,
            GroupSong => interrupt ? FourCC.SongMagicInterrupt : FourCC.SongMagicCast,
            GroupNinjutsu => interrupt ? FourCC.NinjutsuMagicInterrupt : FourCC.NinjutsuMagicCast,
            GroupSummoning => interrupt ? FourCC.SummonMagicInterrupt : FourCC.SummonMagicCast,
            GroupGeomancy => interrupt ? FourCC.GeomancyMagicInterrupt : FourCC.GeomancyMagicCast,
            GroupTrust => interrupt ? FourCC.TrustMagicInterrupt : FourCC.TrustMagicCast,
            _ => FourCC.WhiteMagicInterrupt,
        };

        // the folder of its script under scripts/actions/spells
        public string Folder => Group switch
        {
            GroupWhite => "white",
            GroupBlack => "black",
            GroupSong => "songs",
            GroupNinjutsu => "ninjutsu",
            GroupSummoning => "summoning",
            GroupBlue => "blue",
            GroupGeomancy => "geomancy",
            GroupTrust => "trust",
            _ => "",
        };

        public string ScriptFile => $"./scripts/actions/spells/{Folder}/{Name}.lua";

        public Spell Clone() => (Spell)MemberwiseClone();
    }

    public sealed partial class ZoneHost
    {
        private const string SpellType = "CSpell";
        internal const ushort SpellTargetSelf = 0x01, SpellTargetEnemy = 0x04;

        private Dictionary<ushort, Spell> spells_;
        private readonly HashSet<string> spellScripts_ = new HashSet<string>(StringComparer.Ordinal);

        // spell::LoadSpellList (the spell rows; blue_spell_list stays with GetBlueSpell)
        private void LoadSpells()
        {
            if (spells_ != null) return;
            spells_ = new Dictionary<ushort, Spell>();
            var r = Exec("SELECT spellid, name, jobs, `group`, family, validTargets, skill, castTime, recastTime, animation, animationTime, mpCost, " +
                         "AOE, base, element, zonemisc, multiplier, message, magicBurstMessage, CE, VE, requirements, content_tag, spell_range, radius, " +
                         "status_effect, status_effect_tier FROM spell_list");
            while (r != null && r.Next())
            {
                var s = new Spell
                {
                    Id = r.Get<ushort>("spellid"),
                    Name = r.Get<string>("name"),
                    Group = r.Get<byte>("group"),
                    Family = r.Get<ushort>("family"),
                    ValidTargets = r.Get<ushort>("validTargets"),
                    Skill = r.Get<byte>("skill"),
                    CastTimeMs = r.Get<ushort>("castTime"),
                    RecastTimeMs = r.Get<uint>("recastTime"),
                    Animation = r.Get<ushort>("animation"),
                    AnimationTimeMs = r.Get<ushort>("animationTime"),
                    MpCost = r.Get<ushort>("mpCost"),
                    Aoe = r.Get<byte>("AOE"),
                    Base = r.Get<ushort>("base"),
                    Element = r.Get<byte>("element"),
                    ZoneMisc = r.Get<ushort>("zonemisc"),
                    Multiplier = r.Get<float>("multiplier"),
                    Message = r.Get<ushort>("message"),
                    MagicBurstMessage = (ushort)r.Get<short>("magicBurstMessage"),
                    CE = (int)r.Get<uint>("CE"),
                    VE = (int)r.Get<uint>("VE"),
                    Requirements = (byte)r.Get<sbyte>("requirements"),
                    ContentTag = r.IsNull("content_tag") ? "" : r.Get<string>("content_tag"),
                    Range = r.Get<ushort>("spell_range") / 10f,
                    Radius = r.Get<ushort>("radius") / 10f,
                    StatusEffect = r.IsNull("status_effect") ? null : r.Get<ushort>("status_effect"),
                    StatusEffectTier = r.Get<byte>("status_effect_tier"),
                };
                if (!r.IsNull("jobs"))
                {
                    var jobs = r.GetBlobBytes("jobs");
                    Array.Copy(jobs, 0, s.Jobs, 1, Math.Min(jobs.Length, 22));
                }
                if (s.Folder == "") log_($"spell::LoadSpellList: Spell {s.Name} doesnt have a SpellGroup");
                spells_[s.Id] = s;
            }
            r = Exec("SELECT spellid, knockback FROM blue_spell_list");
            while (r != null && r.Next())
                if (!r.IsNull("knockback") && spells_.TryGetValue(r.Get<ushort>("spellid"), out var blue))
                    blue.Knockback = (byte)r.Get<ushort>("knockback");
        }

        // spell::GetSpell: the row, its script loaded on first use (C++ loads them all with the list)
        private Spell GetSpell(int id)
        {
            LoadSpells();
            if (id < 0 || id > ushort.MaxValue || !spells_.TryGetValue((ushort)id, out var s)) return null;
            if (scripts_ != null && s.Folder != "" && spellScripts_.Add(s.ScriptFile)) scripts_.LoadLuaObjectFromFile(s.ScriptFile);
            return s;
        }

        // ---- the CSpell Lua type -----------------------------------------------------------------------------------

        private void RegisterSpellType()
        {
            static Spell S(LuaState l) => l.ToObject(1) as Spell ?? throw new InvalidOperationException("bad self: expected " + SpellType);
            // luautils::GetSpell: the spell list's own row (nil for none)
            lua_.SetGlobal("GetSpell", l =>
            {
                l.Push(GetSpell(l.ToIntArg(1)));
                return 1;
            });
            lua_.RegisterType(SpellType, new Dictionary<string, LuaFunction>
            {
                ["setMsg"] = l =>
                {
                    S(l).Message = (ushort)l.ToEnumArg(2); // MsgBasic
                    return 0;
                },
                ["setModifier"] = l =>
                {
                    S(l).Modifier = (uint)l.ToEnumArg(2); // ActionModifier
                    return 0;
                },
                ["setCritical"] = l =>
                {
                    S(l).Critical = l.ToBoolean(2);
                    return 0;
                },
                ["setAoE"] = l =>
                {
                    S(l).Aoe = (byte)l.ToIntArg(2);
                    return 0;
                },
                ["setFlag"] = l =>
                {
                    S(l).Flag = (byte)l.ToIntArg(2);
                    return 0;
                },
                ["setRadius"] = l =>
                {
                    S(l).Radius = (float)l.ToNumber(2);
                    return 0;
                },
                ["setAnimation"] = l =>
                {
                    S(l).Animation = (ushort)l.ToIntArg(2);
                    return 0;
                },
                ["setCastTime"] = l =>
                {
                    S(l).CastTimeMs = l.ToUIntArg(2);
                    return 0;
                },
                ["setMPCost"] = l =>
                {
                    S(l).MpCost = (ushort)l.ToIntArg(2);
                    return 0;
                },
                ["isAoE"] = l => Ret(l, S(l).Aoe == Spell.AoeRadial),
                ["getAoE"] = l => Ret(l, S(l).Aoe),
                ["isConal"] = l => Ret(l, S(l).Aoe == Spell.AoeConal),
                ["getRadius"] = l => Ret(l, S(l).Radius),
                ["tookEffect"] = l => Ret(l, S(l).TookEffect),
                ["getMagicBurstMessage"] = l => Ret(l, S(l).MagicBurstMessage),
                ["getElement"] = l => Ret(l, S(l).Element),
                ["getTotalTargets"] = l => Ret(l, S(l).TotalTargets),
                ["getSkillType"] = l => Ret(l, S(l).Skill),
                ["getID"] = l => Ret(l, S(l).Id),
                ["getMPCost"] = l => Ret(l, S(l).MpCost),
                ["getSpellGroup"] = l => Ret(l, S(l).Group),
                ["getSpellFamily"] = l => Ret(l, S(l).Family),
                ["getFlag"] = l => Ret(l, S(l).Flag),
                ["getLevel"] = l => Ret(l, S(l).Job(l.ToEnumArg(2))), // xi::Job
                ["getCastTime"] = l => Ret(l, S(l).CastTimeMs),
                ["getPrimaryTargetID"] = l => Ret(l, S(l).PrimaryTargetId),
                ["getKnockback"] = l => Ret(l, S(l).Knockback),
                ["isCritical"] = l => Ret(l, S(l).Critical),
            });
        }
    }
}
