using System;
using System.Collections.Generic;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // A PUP's automaton out: petutils::LoadPet's automaton (a CAutomatonEntity with the master's equipment, look and
    // name), CalculateAutomatonStats with the pet (the PUP level, LoadAutomatonStats into the pet: the skills of
    // puppetutils::CalculateAutomatonSkills, the frame stats, the weapons from the skills, the frame's evasion and
    // defense, the job point pet mods; onPetStatCalculate, the Optimization / Fine-Tuning merits, FinalizePetStatistics),
    // the attachment scripts (xi.actions.abilities.pets.attachments.<name>: onEquip, onUnequip, onManeuverGain /
    // onManeuverLose from a maneuver gained or lost, onUpdate), the burden (its decay on the master's regen tick, the
    // overload chance), the extended job packet on an update, the maneuvers gone with it on its death, and the
    // bindings (addBurden, getOverloadChance, reduceBurden, hasAttachment, hasAttachmentSet, getAttachments,
    // setAttachment, updateAttachments, unlockAttachment, isExceedingElementalCapacity, the automaton's name, frame
    // and head).
    public sealed partial class ZoneHost
    {
        private const ushort MeritOptimization = 0x0C46, MeritFineTuning = 0x0C44; // xi::Merit
        private const byte PetModAutomaton = 3, PetModStormwaker = 7;
        private const int SkillHealing = 33;
        private const int JpCooldownEffect = 0x240 + 0x07; // JP_COOLDOWN_EFFECT: burden -1

        // puppetutils::CalculateAutomatonSkills: the master's automaton skills capped at the frame and head's caps
        // (the capped bit set at the cap), then the merit and gear bonuses; the magic skill copied into the healing,
        // enhancing, enfeebling, elemental and dark skills. The array is the master's (automatonInfo_.automatonSkills).
        private ushort[] CalculateAutomatonSkills(CharData master, int mlvl)
        {
            var skills = master.Automaton.Skills;
            int Cap(int skill) => AutomatonSkillCap(master, skill, mlvl);
            skills[SkillAutomatonMelee] = (ushort)Math.Min(Cap(SkillAutomatonMelee), master.WorkingSkills[SkillAutomatonMelee] & 0x7FFF);
            skills[SkillAutomatonRanged] = (ushort)Math.Min(Cap(SkillAutomatonRanged), master.WorkingSkills[SkillAutomatonRanged] & 0x7FFF);
            skills[SkillAutomatonMagic] = (ushort)Math.Min(Cap(SkillAutomatonMagic), master.WorkingSkills[SkillAutomatonMagic] & 0x7FFF);

            // Set capped flags
            for (int i = 22; i <= 24; ++i)
                if ((skills[i] & 0x7FFF) == Cap(i)) skills[i] |= 0x8000;

            int meritBonus = master.Merits?.Value(master, MeritAutomatonSkills) ?? 0;
            int magicBonus = master.GetMod(Mod.AUTO_MAGIC_SKILL), meleeBonus = master.GetMod(Mod.AUTO_MELEE_SKILL),
                rangedBonus = master.GetMod(Mod.AUTO_RANGED_SKILL);
            skills[SkillAutomatonMagic] = (ushort)(skills[SkillAutomatonMagic] + magicBonus + meritBonus);
            // copy magic skills into generic skills
            for (int i = SkillHealing; i <= Skill.DarkMagic; i++)
                if (i != Skill.DivineMagic) skills[i] = skills[SkillAutomatonMagic];
            skills[SkillAutomatonRanged] = (ushort)(skills[SkillAutomatonRanged] + meritBonus + rangedBonus);
            skills[SkillAutomatonMelee] = (ushort)(skills[SkillAutomatonMelee] + meritBonus + meleeBonus);
            return skills;
        }

        // petutils::CalculateAutomatonStats with an automaton: the PUP level (with AUTOMATON_LVL_BONUS) or the sub
        // level, LoadAutomatonStats, onPetStatCalculate, the merits, FinalizePetStatistics
        private void CalculatePetAutomatonStats(CharData master, AutomatonEntity pet, PetInfo data)
        {
            int mainLevel = (byte)(master.MJob == JobPupId ? master.MLvl + master.GetMod(Mod.AUTOMATON_LVL_BONUS) : master.SLvl);
            // TEMP: should be MLevel when unsummoned, and PUP level when summoned
            SetPetLevels(pet, mainLevel, mainLevel / 2);
            LoadAutomatonStatsIntoPet(master, pet, data, mainLevel);
            CallPetGlobal("xi.pets.automaton.onPetStatCalculate", master, pet);
            int optimization = master.Merits?.Value(master, MeritOptimization) ?? 0;
            int fineTuning = master.Merits?.Value(master, MeritFineTuning) ?? 0;
            pet.AddModifier(Mod.ATTP, (short)optimization);
            pet.AddModifier(Mod.DEFP, (short)optimization);
            pet.AddModifier(Mod.MATT, (short)optimization);
            pet.AddModifier(Mod.ACC, (short)fineTuning);
            pet.AddModifier(Mod.RACC, (short)fineTuning);
            pet.AddModifier(Mod.EVA, (short)fineTuning);
            pet.AddModifier(Mod.MDEF, (short)fineTuning);
            FinalizePetStatistics(master, pet);
        }

        // petutils::LoadAutomatonStats with a pet: the master's automaton health and stats (the same frame stats and
        // repair kit / mana tank boosts as the equipment menu), the skills, the equipment, look and name; the melee
        // weapon from the automaton melee skill and the pet's delay, the ranged one from the ranged skill; the hard to
        // interrupt 85%, the frame's evasion and defense ranks (half the skill cap), the PUP's job point pet bonuses
        private void LoadAutomatonStatsIntoPet(CharData master, AutomatonEntity pet, PetInfo data, int mlvl)
        {
            var skills = CalculateAutomatonSkills(master, mlvl);
            CalculateAutomatonStats(master);
            var a = master.Automaton;

            Array.Copy(skills, pet.WorkingSkills, pet.WorkingSkills.Length);
            for (int i = 0; i < 7; i++) pet.Stats[i] = a.Stats[i];
            pet.BaseMaxHp = (short)a.MaxHp;
            pet.Hp = a.MaxHp;
            pet.BaseMaxMp = (short)a.MaxMp;
            pet.Mp = a.MaxMp;

            pet.SetEquip(a);
            pet.Look = new ushort[10];
            pet.Look[0] = (ushort)ModelType.Automaton;
            pet.Look[1] = a.ModelId;
            pet.Name = a.Name;

            pet.WeaponSkill = (byte)SkillAutomatonMelee;
            pet.MainDelay = pet.BaseDelay = data.CmbDelay; // every pet should use this eventually
            pet.MainDamage = (ushort)Math.Floor(pet.WorkingSkills[SkillAutomatonMelee] / 8.7f * 2.0f + 3.0f);
            // (the ranged weapon's skill and piercing damage type: mobs keep no ranged weapon item)
            pet.RangedDamage = (ushort)Math.Floor(pet.WorkingSkills[SkillAutomatonRanged] / 8.7f * 2.0f + 3.0f);

            // Automatons are hard to interrupt
            pet.AddModifier(Mod.SPELLINTERRUPT, 85);

            // Base Automaton Defense Ranks // Cap at 1/2 the ranks skill cap // These get modified by a frame-specific DEFP Modifier.
            var growth = loader_.Stats.Growth;
            int level = Math.Min(mlvl, 99);
            (int evaRank, int defRank) = pet.Frame switch
            {
                0x21 => (7, 5),  // Valoredge: B-
                0x22 => (2, 8),  // Sharpshot: C-
                0x23 => (10, 9), // Stormwaker: D
                _ => (4, 7),     // Harlequin: C
            };
            pet.WorkingSkills[Skill.Evasion] = (ushort)growth.GetMaxSkillByRank(evaRank, level);
            pet.SetModifier(Mod.DEF, (short)(growth.GetMaxSkillByRank(defRank, level) / 2));

            // Add Job Point Stat Bonuses
            if (master.MJob == JobPupId)
            {
                pet.AddModifier(Mod.ATT, (short)master.GetMod(Mod.PET_ATK_DEF));
                pet.AddModifier(Mod.DEF, (short)master.GetMod(Mod.PET_ATK_DEF));
                pet.AddModifier(Mod.ACC, (short)master.GetMod(Mod.PET_ACC_EVA));
                pet.AddModifier(Mod.EVA, (short)master.GetMod(Mod.PET_ACC_EVA));
                pet.AddModifier(Mod.MATT, (short)master.GetMod(Mod.PET_MAB_MDB));
                pet.AddModifier(Mod.MDEF, (short)master.GetMod(Mod.PET_MAB_MDB));
                pet.AddModifier(Mod.MACC, (short)master.GetMod(Mod.PET_MACC_MEVA));
                pet.AddModifier(Mod.MEVA, (short)master.GetMod(Mod.PET_MACC_MEVA));
            }
        }

        // ---- the attachments -----------------------------------------------------------------------------------------

        // luautils::OnAttachmentEquip / OnAttachmentUnequip / OnManeuverGain / OnManeuverLose / OnUpdateAttachment:
        // xi.actions.abilities.pets.attachments.<name>.<hook>(automaton, the attachment item[, maneuvers])
        private void AttachmentHook(AutomatonEntity pet, byte attachment, string hook, int? maneuvers = null)
        {
            if (lua_ == null || scripts_ == null) return;
            var item = LookupItem((ushort)(0x2100 + attachment));
            if (item == null) return;
            string file = $"./scripts/actions/abilities/pets/attachments/{item.Name}.lua";
            if (abilityScripts_.Add(file)) scripts_.LoadLuaObjectFromFile(file);
            int top = lua_.Top;
            try
            {
                lua_.GetPath($"xi.actions.abilities.pets.attachments.{item.Name}.{hook}");
                if (!lua_.IsFunction(-1)) return;
                lua_.PushObject(pet);
                lua_.PushObject(item, ItemType);
                if (maneuvers is int count) lua_.Push((double)count);
                lua_.Call(maneuvers == null ? 2 : 3, 0);
            }
            catch (LuaException e)
            {
                log_($"luautils::{hook}: {e.Message}");
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // puppetutils::EquipAttachments
        private void EquipAttachments(AutomatonEntity pet)
        {
            for (int i = 0; i < 12; i++)
                if (pet.Attachments[i] != 0 && PuppetItem(0x2100 + pet.Attachments[i]) != null) AttachmentHook(pet, pet.Attachments[i], "onEquip");
        }

        // puppetutils::CheckAttachmentsForManeuver: the attachments with a slot of the maneuver's element hear of it,
        // with the master's count of that maneuver
        private void CheckAttachmentsForManeuver(CharData c, ushort maneuver, bool gain)
        {
            if (c.PPet is not AutomatonEntity pet) return;
            int element = maneuver - EffectId.FireManeuver;
            for (int i = 0; i < 12; i++)
            {
                if (pet.Attachments[i] == 0 || PuppetItem(0x2100 + pet.Attachments[i]) is not { } puppet) continue;
                if (ElementPower(puppet.Element, element) != 0)
                    AttachmentHook(pet, pet.Attachments[i], gain ? "onManeuverGain" : "onManeuverLose", c.StatusEffects.GetEffectsCount(maneuver));
            }
        }

        // puppetutils::UpdateAttachments: each attachment's onUpdate with the master's count of its first element's
        // maneuver
        private void UpdateAttachments(CharData c)
        {
            if (c.PPet is not AutomatonEntity pet) return;
            for (int i = 0; i < 12; i++)
            {
                if (pet.Attachments[i] == 0 || PuppetItem(0x2100 + pet.Attachments[i]) is not { } puppet) continue;
                ushort maneuver = EffectId.FireManeuver;
                for (int j = 0; j < 8; j++)
                {
                    if (ElementPower(puppet.Element, j) != 0)
                    {
                        maneuver = (ushort)(maneuver + j);
                        break;
                    }
                }
                AttachmentHook(pet, pet.Attachments[i], "onUpdate", c.StatusEffects.GetEffectsCount(maneuver));
            }
        }

        // a maneuver gained or lost by a char (CStatusEffectContainer::AddStatusEffect / RemoveStatusEffect)
        private void OnManeuverChange(BattleEntity owner, ushort effectId, bool gain)
        {
            if (owner is CharData c && effectId >= EffectId.FireManeuver && effectId <= EffectId.DarkManeuver) CheckAttachmentsForManeuver(c, effectId, gain);
        }

        // ---- bindings ------------------------------------------------------------------------------------------------

        private static int PushNilRet(LuaState l)
        {
            l.PushNil();
            return 1;
        }

        private void RegisterAutomatonBindings(Dictionary<string, LuaFunction> m)
        {
            AutomatonEntity Automaton(LuaState l) => (Entity(l) as BattleEntity)?.PPet as AutomatonEntity;
            m["addBurden"] = l => Ret(l, Automaton(l) is { } pet
                ? pet.AddBurden((BattleEntity)Entity(l), (byte)l.ToIntArg(2), (sbyte)(byte)l.ToIntArg(3), MobRng)
                : (byte)0);
            m["getOverloadChance"] = l => Ret(l, Automaton(l) is { } pet ? pet.OverloadChance((BattleEntity)Entity(l), (byte)l.ToIntArg(2)) : (byte)0);
            m["reduceBurden"] = l =>
            {
                if (Entity(l) is not CharData c || c.PPet is not AutomatonEntity pet) return 0;
                float percentReduction = (float)l.ToNumber(2);
                byte intReduction = l.IsNil(3) ? (byte)0 : (byte)l.ToIntArg(3);
                int cooldown = JobPointUtil.Value(c, JpCooldownEffect);
                for (int i = 0; i < 8; i++)
                    pet.Burden[i] = (byte)Math.Max(0.0f, pet.Burden[i] * (1 - (percentReduction / 100 - cooldown)) - intReduction);
                return 0;
            };
            m["isExceedingElementalCapacity"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                for (int i = 0; i < 8; i++)
                    if (c.Automaton.ElementEquip[i] > c.Automaton.ElementMax[i]) return Ret(l, true);
                return Ret(l, false);
            };
            m["hasAttachment"] = l =>
            {
                if (Entity(l) is not CharData c) return Ret(l, false);
                var puppet = PuppetItem(l.ToIntArg(2));
                return Ret(l, puppet != null && HasPuppetUnlock(c, l.ToIntArg(2), puppet.Value.Slot));
            };
            m["hasAttachmentSet"] = l => Ret(l, Entity(l) is AutomatonEntity pet && pet.HasAttachment((byte)(l.ToIntArg(2) - 0x2100)));
            m["getAutomatonName"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    l.PushString("");
                    return 1;
                }
                var r = Exec("SELECT name FROM char_pet LEFT JOIN pet_name ON automatonid = id WHERE charid = ? LIMIT 1", BoundValue.Of(c.Id));
                l.PushString(r != null && r.Next() && !r.IsNull("name") ? r.Get<string>("name") : "");
                return 1;
            };
            m["getAutomatonFrame"] = l => Entity(l) switch
            {
                CharData c when c.Automaton.Frame >= AutomatonInfo.FrameHarlequin && c.Automaton.Frame <= AutomatonInfo.FrameStormwaker => Ret(l, c.Automaton.Frame),
                AutomatonEntity pet => Ret(l, pet.Frame),
                _ => PushNilRet(l),
            };
            m["getAutomatonHead"] = l => Entity(l) switch
            {
                CharData c when c.Automaton.Head >= AutomatonInfo.HeadHarlequin && c.Automaton.Head <= AutomatonInfo.HeadSpiritreaver => Ret(l, c.Automaton.Head),
                AutomatonEntity pet => Ret(l, pet.Head),
                _ => PushNilRet(l),
            };
            m["setAutomatonFrame"] = l =>
            {
                if (Entity(l) is not CharData c) return 0;
                SetFrame(c, (byte)l.ToEnumArg(2)); // AutomatonFrame
                if (SessionOf(c) is { } s) SendExtendedJobPackets(s, c);
                SaveAutomaton(c);
                return 0;
            };
            m["setAutomatonHead"] = l =>
            {
                if (Entity(l) is not CharData c) return 0;
                SetHead(c, (byte)l.ToEnumArg(2)); // AutomatonHead
                if (SessionOf(c) is { } s) SendExtendedJobPackets(s, c);
                SaveAutomaton(c);
                return 0;
            };
            m["setAttachment"] = l =>
            {
                if (Entity(l) is not CharData c) return 0;
                SetAttachment(c, (byte)l.ToIntArg(3), (byte)(l.ToIntArg(2) - 0x2100));
                if (SessionOf(c) is { } s) SendExtendedJobPackets(s, c);
                SaveAutomaton(c);
                return 0;
            };
            m["updateAttachments"] = l =>
            {
                if (Entity(l) is CharData c) UpdateAttachments(c);
                return 0;
            };
            m["unlockAttachment"] = l => Ret(l, Entity(l) is CharData c && UnlockAttachment(c, l.ToIntArg(2)));
            m["getAttachments"] = l =>
            {
                if (Entity(l) is not AutomatonEntity pet)
                {
                    log_("Invalid Entity accessing function.");
                    return PushNilRet(l);
                }
                l.NewTable();
                for (int slot = 0; slot < 12; slot++)
                {
                    byte attachment = pet.Attachments[slot];
                    string name = attachment != 0 && PuppetItem(0x2100 + attachment) != null ? LookupItem((ushort)(0x2100 + attachment))?.Name ?? "" : "";
                    l.Push((double)slot);
                    l.PushString(name);
                    l.SetTable(-3);
                }
                return 1;
            };
        }

        // puppetutils::UnlockAttachment: a puppet item sets its unlock bit (the attachment's id & 0xFF, a frame's or
        // head's id & 0x0F) when not yet set; then the unlocks are saved and the extended job packets sent
        private bool UnlockAttachment(CharData c, int itemId)
        {
            if (PuppetItem(itemId) is not { } puppet) return false;
            var a = c.Automaton;
            // addBit: false when out of range or already set
            static bool AddBit(int bit, byte[] bits)
            {
                if (bit >= bits.Length * 8 || (bits[bit >> 3] & 1 << (bit & 7)) != 0) return false;
                bits[bit >> 3] |= (byte)(1 << (bit & 7));
                return true;
            }
            bool added;
            switch (puppet.Slot)
            {
                case PuppetSlotAttachment:
                    added = AddBit(itemId & 0xFF, a.UnlockedAttachments);
                    break;
                case PuppetSlotFrame:
                {
                    var bits = new[] { a.UnlockedFrames };
                    added = AddBit(itemId & 0x0F, bits);
                    a.UnlockedFrames = bits[0];
                    break;
                }
                case PuppetSlotHead:
                {
                    var bits = new[] { a.UnlockedHeads };
                    added = AddBit(itemId & 0x0F, bits);
                    a.UnlockedHeads = bits[0];
                    break;
                }
                default:
                    return false;
            }
            if (!added) return false;
            Exec("UPDATE char_pet SET unlocked_attachments = ? WHERE charid = ? LIMIT 1", BoundValue.Of(a.UnlockedBlob()), BoundValue.Of(c.Id));
            if (SessionOf(c) is { } s) SendExtendedJobPackets(s, c);
            return true;
        }
    }
}
