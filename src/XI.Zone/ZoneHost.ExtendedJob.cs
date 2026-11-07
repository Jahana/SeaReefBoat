using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // GP_CLI_COMMAND_EXTENDED_JOB (0x102) and what it drives: blueutils (the set blue magic), puppetutils (the
    // automaton's head, frame and attachments) and the 0x044 GP_SERV_COMMAND_EXTENDED_JOB packets; a blue magic change
    // rebuilds the traits (BuildingCharTraitsTable, ZoneHost.Traits) and puts every set blue spell on a 60 s recast.
    // A PUP change is refused while the automaton is deployed; the deployed automaton's skills and stats
    // (CalculateAutomatonSkills, FinalizePetStatistics) are ZoneHost.Automatons'. A Monipulator's equipment change in
    // the Feretory is in ZoneHost.Monstrosity (HandleEquipChangePacket).
    public sealed partial class ZoneHost
    {
        private const byte JobBluId = 16, JobPupId = 18;
        private const int BlueSpellOffset = 0x200;
        private const long BlueSetRecastMs = 60_000;
        private const ushort MeritAutomatonSkills = 0x05C0; // Merit::AutomatonSkills
        private const byte PuppetSlotHead = 1, PuppetSlotFrame = 2, PuppetSlotAttachment = 3; // ITEM_PUPPET_EQUIPSLOT
        private const byte AttachmentOpticFiber = 198, AttachmentOpticFiberII = 206;
        private static readonly byte[] AutoRepairKits = { 193, 196, 202, 205 }, ManaTanks = { 225, 228, 233, 235 };
        private const int SkillAutomatonMelee = 22, SkillAutomatonRanged = 23, SkillAutomatonMagic = 24;

        private void RegisterExtendedJobHandler()
        {
            Register(0x102, (s, c, p) => ValidateExtendedJob(c, p), (s, c, p) => OnExtendedJob(s, c, p));
        }

        // the union's three arms: blu_data_t (SpellId u8 @4, JobIndex @8, Spells[20] @12), pup_data_t (ItemId u8 @4,
        // JobIndex @8, Slots[14] @12: head, frame, attachments 1-12) and mon_data_t (Flags0 @0x0A, SpeciesIndex @0x0C,
        // Slots u16[12] @0x10, Desc1Index @0x28, Desc2Index @0x29)
        private static bool IsBluPacket(CharData c, BasicPacket p) => (c.MJob == JobBluId || c.SJob == JobBluId) && p.Buffer[8] == JobBluId;
        private static bool IsPupPacket(CharData c, BasicPacket p) => (c.MJob == JobPupId || c.SJob == JobPupId) && p.Buffer[8] == JobPupId;
        private static bool IsMonPacket(CharData c) => c.Zone == Monstrosity.ZoneFeretory && c.Monstrosity != null;

        private PacketValidator ValidateExtendedJob(CharData c, BasicPacket p)
        {
            var pv = Pv(c).BlockedBy(BlockedState.InEvent);
            // BLU: owning the spells is not checked (as C++). PUP: not while an automaton is deployed.
            if (IsBluPacket(c, p)) return pv;
            if (IsPupPacket(c, p)) return pv.MustEqual(c.PPet is AutomatonEntity ? 1 : 0, 0, "Player has a deployed automaton.");
            if (IsMonPacket(c))
            {
                // a Monipulator's equipment change: a species, instincts (3-799, 0xFFFF to unequip) or name descriptors (0-248)
                byte flags = p.Buffer[0x0A];
                if ((flags & MonFlagSpecies) != 0) pv.Range("SpeciesIndex", BitConverter.ToUInt16(p.Buffer, 0x0C), 1, 511);
                if ((flags & MonFlagInstinct) != 0)
                    for (int i = 0; i < 12; i++)
                    {
                        ushort slot = BitConverter.ToUInt16(p.Buffer, 0x10 + i * 2);
                        if (slot != 0xFFFF && slot != 0) pv.Range("Slots", slot, 3, 799);
                    }
                if ((flags & MonFlagDescriptor1) != 0) pv.Range("Descriptor1Index", p.Buffer[0x28], 0, 248);
                if ((flags & MonFlagDescriptor2) != 0) pv.Range("Descriptor2Index", p.Buffer[0x29], 0, 248);
                return pv;
            }
            return new PacketValidator(c).AddError("Did not match any of the extended job system.");
        }

        private void OnExtendedJob(MapSession s, CharData c, BasicPacket p)
        {
            if (IsBluPacket(c, p))
            {
                // whatever the change was, every set blue spell goes on a 60 s recast
                if (OnBlueMagicSet(s, c, p))
                    for (int i = 0; i < 20; i++)
                        if (c.SetBlueSpells[i] != 0 && GetBlueSpell(c.SetBlueSpells[i] + BlueSpellOffset) is { } set)
                            RecastsOf(c).Add(RecastType.Magic, set.Id, BlueSetRecastMs);
            }
            else if (IsPupPacket(c, p))
            {
                byte itemId = p.Buffer[4];
                if (itemId == 0)
                {
                    // remove every attachment named; the packet's first two slots are the head and frame
                    for (int i = 2; i < 14; i++)
                        if (p.Buffer[12 + i] != 0) SetAttachment(c, (byte)(i - 2), 0);
                }
                else if (p.Buffer[12] != 0) SetHead(c, p.Buffer[12]);
                else if (p.Buffer[13] != 0) SetFrame(c, p.Buffer[13]);
                else
                {
                    for (int i = 2; i < 14; i++)
                        if (p.Buffer[12 + i] != 0) SetAttachment(c, (byte)(i - 2), p.Buffer[12 + i]);
                }

                CalculateAutomatonStats(c);
                SendExtendedJobPackets(s, c);
                SaveAutomaton(c);
            }
            else if (IsMonPacket(c)) MonstrosityEquipChange(c, p);
        }

        // false when the packet is refused before the recasts
        private bool OnBlueMagicSet(MapSession s, CharData c, BasicPacket p)
        {
            byte spellToAdd = p.Buffer[4];
            if (spellToAdd == 0)
            {
                for (int i = 0; i < 20; i++)
                {
                    byte id = p.Buffer[12 + i];
                    if (id == 0) continue;
                    var spell = GetBlueSpell(id + BlueSpellOffset);
                    if (spell == null)
                    {
                        log_($"zone: GP_CLI_COMMAND_EXTENDED_JOB: Cannot resolve spell id {id}");
                        return false;
                    }
                    if (c.SetBlueSpells[i] == 0)
                    {
                        log_($"zone: GP_CLI_COMMAND_EXTENDED_JOB: Player {c.Name} trying to unset BLU spell they don't have set!");
                        return false;
                    }
                    SetBlueSpell(c, spell, (byte)i, false);
                }
                PushBlueMagicChange(s, c);
            }
            else
            {
                int index = -1;
                byte id = 0;
                for (int i = 0; i < 20; i++)
                {
                    if (p.Buffer[12 + i] == 0) continue;
                    id = p.Buffer[12 + i];
                    index = i;
                    break;
                }

                if (index == -1) log_("zone: No match found.");
                else if (GetBlueSpell(id + BlueSpellOffset) is not BlueSpell spell) log_($"zone: GP_CLI_COMMAND_EXTENDED_JOB: Cannot resolve spell id {id}");
                else
                {
                    int mLevel = c.LevelRestriction != 0 && c.LevelRestriction < c.MLvl ? (int)c.LevelRestriction : c.MLvl, sLevel = mLevel / 2;
                    if (mLevel < spell.Job(c.MJob) && sLevel < spell.Job(c.SJob))
                    {
                        log_($"zone: GP_CLI_COMMAND_EXTENDED_JOB: Player {c.Name} trying to set BLU spell at invalid level!");
                        return false;
                    }
                    SetBlueSpell(c, spell, (byte)index, true);
                    PushBlueMagicChange(s, c);
                }
            }
            return true;
        }

        // after a set spell changed: BuildingCharTraitsTable, COMMAND_DATA, the extended job packets, CLISTATUS, and
        // UpdateHealth
        private void PushBlueMagicChange(MapSession s, CharData c)
        {
            BuildCharTraits(c);
            Push(s, LoginPackets.CommandData(c));
            SendExtendedJobPackets(s, c);
            if (loader_.Stats is CharStats stats) Push(s, SelfPackets.CliStatus(c, stats, UnityStatus(c)));
            CharStats.UpdateHealth(c);
        }

        // ---- spell::GetSpell for blue magic -----------------------------------------------------------------------

        private sealed class BlueSpell
        {
            public ushort Id;
            public readonly byte[] Jobs = new byte[CharData.MaxJobs]; // the level per job, by job id
            // CSpell::getJob: a job without the spell (CANNOT_USE_SPELL, 0) reads 255
            public byte Job(int job) => Jobs[job] == 0 ? (byte)255 : Jobs[job];
            public ushort SetPoints;
            public byte TraitCategory, TraitWeight; // blueutils::CalculateTraits' category and its weight
            public byte PrimarySc, SecondarySc;      // the skillchain properties Chain Affinity / Azure Lore open with
            public readonly List<(int Mod, short Value)> Mods = new List<(int, short)>();
        }

        private Dictionary<ushort, BlueSpell> blueSpells_;

        // a spell_list row of SPELLGROUP_BLUE (3): its jobs blob, the set points of its blue_spell_list row (left 0 when
        // the row's content is disabled) and its blue_spell_mods. Another group's spell resolves to nothing here, where
        // C++ static_casts it to CBlueSpell.
        private BlueSpell GetBlueSpell(int id)
        {
            if (blueSpells_ == null)
            {
                blueSpells_ = new Dictionary<ushort, BlueSpell>();
                var r = Exec("SELECT spellid, jobs FROM spell_list WHERE `group` = 3");
                while (r != null && r.Next())
                {
                    var spell = new BlueSpell { Id = r.Get<ushort>("spellid") };
                    if (!r.IsNull("jobs"))
                    {
                        var jobs = r.GetBlobBytes("jobs");
                        Array.Copy(jobs, 0, spell.Jobs, 1, Math.Min(jobs.Length, 22));
                    }
                    blueSpells_[spell.Id] = spell;
                }
                r = Exec("SELECT blue_spell_list.spellid, blue_spell_list.set_points, blue_spell_list.trait_category, blue_spell_list.trait_category_weight, " +
                         "blue_spell_list.primary_sc, blue_spell_list.secondary_sc, " +
                         "spell_list.content_tag FROM blue_spell_list JOIN spell_list ON blue_spell_list.spellid = spell_list.spellid");
                while (r != null && r.Next())
                {
                    string tag = r.IsNull("content_tag") ? "" : r.Get<string>("content_tag");
                    if (tag != "" && !contentEnabled_(tag)) continue;
                    if (!blueSpells_.TryGetValue(r.Get<ushort>("spellid"), out var spell)) continue;
                    spell.SetPoints = (ushort)r.Get<short>("set_points");
                    spell.TraitCategory = (byte)r.Get<short>("trait_category");
                    spell.TraitWeight = (byte)r.Get<short>("trait_category_weight");
                    spell.PrimarySc = r.IsNull("primary_sc") ? (byte)0 : (byte)r.Get<short>("primary_sc");
                    spell.SecondarySc = r.IsNull("secondary_sc") ? (byte)0 : (byte)r.Get<short>("secondary_sc");
                }
                r = Exec("SELECT spellId, modid, value FROM blue_spell_mods");
                while (r != null && r.Next())
                    if (blueSpells_.TryGetValue(r.Get<ushort>("spellId"), out var spell))
                        spell.Mods.Add((r.Get<ushort>("modid"), r.Get<short>("value")));
            }
            return id >= 0 && blueSpells_.TryGetValue((ushort)id, out var b) ? b : null;
        }

        private static void ApplyBlueMods(CharData c, BlueSpell spell, int sign)
        {
            foreach (var (mod, value) in spell.Mods)
                if (mod < c.ModStat.Length) c.ModStat[mod] = (short)(c.ModStat[mod] + sign * value);
        }

        // ---- blueutils ----------------------------------------------------------------------------------------------

        private static byte BluLevel(CharData c) => c.MJob == JobBluId ? c.MLvl : c.SJob == JobBluId ? c.SLvl : (byte)0;

        private static int BlueTotalSlots(CharData c)
        {
            int level = BluLevel(c);
            return level == 0 ? 0 : Math.Clamp((level - 1) / 10 * 2 + 6, 6, 20);
        }

        private static int BlueTotalPoints(CharData c)
        {
            int level = BluLevel(c);
            if (level == 0) return 0;
            int points = Math.Clamp((level - 1) / 10 * 5 + 10, 0, 55);
            if (level >= 75) points += c.Merits?.Value(c, MeritTable.Assimilation) ?? 0;
            if (level >= 99) points += JobPointUtil.Value(c, JobPointUtil.BlueMagicPointBonus);
            return (byte)points;
        }

        private static bool IsBlueSpellSet(CharData c, BlueSpell spell)
        {
            foreach (byte set in c.SetBlueSpells)
                if (set != 0 && set == spell.Id - BlueSpellOffset) return true;
            return false;
        }

        private bool HasEnoughSetPoints(CharData c, BlueSpell spell, int slotToPut)
        {
            int points = 0;
            for (int slot = 0; slot < 20; slot++)
                if (slot != slotToPut && c.SetBlueSpells[slot] != 0 && GetBlueSpell(c.SetBlueSpells[slot] + BlueSpellOffset) is BlueSpell set)
                    points += set.SetPoints;
            return (byte)points + spell.SetPoints <= BlueTotalPoints(c);
        }

        // blueutils::SetBlueSpell: the slot's spell (if any) comes out; adding puts the spell in when it is not set
        // elsewhere, the set points allow it and the char knows it. Saved either way.
        private void SetBlueSpell(CharData c, BlueSpell spell, byte slot, bool adding)
        {
            if (adding && slot >= BlueTotalSlots(c))
            {
                log_($"zone: SetBlueSpell: Player {c.Name} trying to set a spell in slot {slot} beyond their slot count");
                return;
            }
            if (slot >= 20 || spell == null) return;

            if (c.SetBlueSpells[slot] != 0)
            {
                if (GetBlueSpell(c.SetBlueSpells[slot] + BlueSpellOffset) is BlueSpell old) ApplyBlueMods(c, old, -1);
                c.SetBlueSpells[slot] = 0;
            }
            if (adding && !IsBlueSpellSet(c, spell) && HasEnoughSetPoints(c, spell, slot))
            {
                if (HasBit(c.SpellList, spell.Id))
                {
                    c.SetBlueSpells[slot] = (byte)(spell.Id - BlueSpellOffset);
                    ApplyBlueMods(c, spell, 1);
                }
                else log_($"zone: SetBlueSpell: Player {c.Name} trying to set spell ID {spell.Id} they don't have!");
            }
            SaveSetSpells(c);
        }

        private void SaveSetSpells(CharData c)
        {
            if (c.MJob != JobBluId && c.SJob != JobBluId) return;
            if (Exec("UPDATE chars SET set_blue_spells = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.SetBlueSpells), BoundValue.Of(c.Id)) == null)
                log_($"zone: Failed to save set blue spells for {c.Name}");
        }

        internal const string SqlSetBlueSpells = "SELECT set_blue_spells FROM chars WHERE charid = ? LIMIT 1";

        // blueutils::LoadSetSpells: a spell that no longer resolves is cleared, the rest add their mods, then validated
        internal void LoadSetSpells(CharData c)
        {
            if (c.MJob != JobBluId && c.SJob != JobBluId) return;
            var r = Exec(SqlSetBlueSpells, BoundValue.Of(c.Id));
            if (r != null && r.RowsCount() > 0 && r.Next() && !r.IsNull("set_blue_spells"))
            {
                var bytes = r.GetBlobBytes("set_blue_spells");
                Array.Clear(c.SetBlueSpells);
                Array.Copy(bytes, c.SetBlueSpells, Math.Min(bytes.Length, 20));
            }
            for (int i = 0; i < 20; i++)
            {
                if (c.SetBlueSpells[i] == 0) continue;
                if (GetBlueSpell(c.SetBlueSpells[i] + BlueSpellOffset) is BlueSpell spell) ApplyBlueMods(c, spell, 1);
                else c.SetBlueSpells[i] = 0;
            }
            ValidateBlueSpells(c);
        }

        // blueutils::ValidateBlueSpells: spells above the BLU level, past the set points or past the slot count come out
        internal void ValidateBlueSpells(CharData c)
        {
            int level = BluLevel(c);
            if (level != 0)
                for (int slot = 0; slot < 20; slot++)
                    if (c.SetBlueSpells[slot] != 0 && GetBlueSpell(c.SetBlueSpells[slot] + BlueSpellOffset) is BlueSpell spell && level < spell.Job(JobBluId))
                        SetBlueSpell(c, spell, (byte)slot, false);

            int maxPoints = BlueTotalPoints(c), current = 0;
            for (int slot = 0; slot < 20; slot++)
            {
                if (c.SetBlueSpells[slot] == 0 || GetBlueSpell(c.SetBlueSpells[slot] + BlueSpellOffset) is not BlueSpell spell) continue;
                if (current + spell.SetPoints > maxPoints) SetBlueSpell(c, spell, (byte)slot, false);
                else current = (byte)(current + spell.SetPoints);
            }

            // CompactSpells
            for (int i = 0; i < 20; i++)
            {
                if (c.SetBlueSpells[i] != 0) continue;
                for (int j = i; j < 20; j++)
                {
                    if (c.SetBlueSpells[j] == 0) continue;
                    c.SetBlueSpells[i] = c.SetBlueSpells[j];
                    c.SetBlueSpells[j] = 0;
                    break;
                }
            }

            for (int slot = BlueTotalSlots(c); slot < 20; slot++)
                if (c.SetBlueSpells[slot] != 0)
                    SetBlueSpell(c, GetBlueSpell(c.SetBlueSpells[slot] + BlueSpellOffset), (byte)slot, false);

            SaveSetSpells(c);
        }

        // blueutils::UnequipAllBlueSpells
        internal void UnequipAllBlueSpells(MapSession s, CharData c)
        {
            for (int i = 0; i < 20; i++)
            {
                if (c.SetBlueSpells[i] == 0) continue;
                var spell = GetBlueSpell(c.SetBlueSpells[i] + BlueSpellOffset);
                c.SetBlueSpells[i] = 0;
                if (spell != null) ApplyBlueMods(c, spell, -1);
            }
            BuildCharTraits(c);
            SendExtendedJobPackets(s, c);
            if (loader_.Stats is CharStats stats) Push(s, SelfPackets.CliStatus(c, stats, UnityStatus(c)));
            loader_.CalculateStats(c);
            CharStats.UpdateHealth(c);
            SaveSetSpells(c);
            c.UpdateMask |= CharStats.UpdateHp;
        }

        // ---- puppetutils --------------------------------------------------------------------------------------------

        private Dictionary<ushort, (byte Slot, uint Element)> puppetItems_;

        // xi::items::lookup<CItemPuppet>: item_puppet's equip slot and element slots (4 bits per element)
        private (byte Slot, uint Element)? PuppetItem(int id)
        {
            if (puppetItems_ == null)
            {
                puppetItems_ = new Dictionary<ushort, (byte, uint)>();
                var r = Exec("SELECT itemid, slot, element FROM item_puppet");
                while (r != null && r.Next()) puppetItems_[r.Get<ushort>("itemid")] = (r.Get<byte>("slot"), r.Get<uint>("element"));
            }
            return puppetItems_.TryGetValue((ushort)id, out var e) ? e : null;
        }

        private static int ElementPower(uint slots, int element) => (int)(slots >> element * 4 & 0xF);

        private static bool BitSet(byte[] bits, int bit) => bit < bits.Length * 8 && (bits[bit >> 3] & 1 << (bit & 7)) != 0;

        // puppetutils::HasAttachment: the unlocked heads, frames or attachments bit for the item
        private static bool HasPuppetUnlock(CharData c, int itemId, byte slot)
        {
            var a = c.Automaton;
            return slot switch
            {
                PuppetSlotAttachment => BitSet(a.UnlockedAttachments, itemId & 0xFF),
                PuppetSlotFrame => BitSet(new[] { a.UnlockedFrames }, itemId & 0x0F),
                PuppetSlotHead => BitSet(new[] { a.UnlockedHeads }, itemId & 0x0F),
                _ => false,
            };
        }

        // puppetutils::setAttachment: an attachment goes in when unlocked, not equipped elsewhere and within every
        // element's capacity (else the old one goes back); 0 takes the slot's attachment out
        private void SetAttachment(CharData c, byte slot, byte attachment)
        {
            var a = c.Automaton;
            var item = PuppetItem(0x2100 + attachment);
            if (attachment != 0)
            {
                if (item is { } i && !HasPuppetUnlock(c, 0x2100 + attachment, i.Slot)) return;
                if (a.HasAttachment(attachment)) return;
            }

            byte old = slot < 12 ? a.Attachments[slot] : (byte)0;
            if (attachment != 0 && old != 0) SetAttachment(c, slot, 0);

            if (attachment != 0)
            {
                if (item is { Slot: PuppetSlotAttachment } attach)
                {
                    bool valid = true;
                    for (int e = 0; e < 8; e++)
                        if (a.ElementEquip[e] + ElementPower(attach.Element, e) > a.ElementMax[e])
                        {
                            valid = false;
                            break;
                        }
                    if (valid)
                    {
                        for (int e = 0; e < 8; e++) a.ElementEquip[e] = (byte)(a.ElementEquip[e] + ElementPower(attach.Element, e));
                        if (slot < 12) a.Attachments[slot] = attachment;
                        return;
                    }
                }
                SetAttachment(c, slot, old);
            }
            else
            {
                attachment = slot < 12 ? a.Attachments[slot] : (byte)0;
                if (attachment == 0 || PuppetItem(0x2100 + attachment) is not { Slot: PuppetSlotAttachment } attach) return;
                for (int e = 0; e < 8; e++) a.ElementEquip[e] = (byte)(a.ElementEquip[e] - ElementPower(attach.Element, e));
                if (slot < 12) a.Attachments[slot] = 0;
            }
        }

        // puppetutils::setFrame / setHead: the old part's element slots come off the maxes and the new one's go on;
        // Harlequin needs no unlock
        private void SetFrame(CharData c, byte frame) => SetPart(c, frame, PuppetSlotFrame, AutomatonInfo.FrameHarlequin);
        private void SetHead(CharData c, byte head) => SetPart(c, head, PuppetSlotHead, AutomatonInfo.HeadHarlequin);

        private void SetPart(CharData c, byte part, byte slot, byte free)
        {
            var a = c.Automaton;
            var max = (byte[])a.ElementMax.Clone();
            byte current = slot == PuppetSlotFrame ? a.Frame : a.Head;
            if (current != 0)
            {
                if (PuppetItem(0x2000 + current) is not { } old || old.Slot != slot) return;
                for (int e = 0; e < 8; e++) max[e] = (byte)(max[e] - ElementPower(old.Element, e));
            }

            if (PuppetItem(0x2000 + part) is not { } item || item.Slot != slot || part != free && !HasPuppetUnlock(c, 0x2000 + part, slot)) return;
            for (int e = 0; e < 8; e++) max[e] = (byte)(max[e] + ElementPower(item.Element, e));

            if (slot == PuppetSlotFrame) a.Frame = part;
            else a.Head = part;
            a.ModelId = AutomatonModel(a.Frame, a.Head);
            max.CopyTo(a.ElementMax, 0);
        }

        // calculateAutomatonModel
        private static ushort AutomatonModel(byte frame, byte head)
        {
            ushort[] models = frame switch
            {
                0x20 => new ushort[] { 0x07B9, 0x07BA, 0x07BC, 0x07BB, 0x07D3, 0x07D7 },
                0x21 => new ushort[] { 0x07BE, 0x07BF, 0x07C1, 0x07C0, 0x07D4, 0x07D8 },
                0x22 => new ushort[] { 0x07C3, 0x07C4, 0x07C6, 0x07C5, 0x07D5, 0x07D9 },
                0x23 => new ushort[] { 0x07C8, 0x07C9, 0x07CB, 0x07CA, 0x07D6, 0x07DA },
                _ => null,
            };
            return models != null && head >= 1 && head <= 6 ? models[head - 1] : (ushort)0x07B9;
        }

        internal const string SqlAutomaton = "SELECT unlocked_attachments, name, equipped_attachments FROM char_pet LEFT JOIN pet_name ON automatonid = id WHERE charid = ?";

        // puppetutils::LoadAutomaton: the unlocks always; for a PUP the name and the equipment, reset to Harlequin
        // when out of range, the head and frame first, then Optic Fibers before the other attachments
        internal void LoadAutomaton(CharData c)
        {
            var r = Exec(SqlAutomaton, BoundValue.Of(c.Id));
            if (r == null || r.RowsCount() == 0 || !r.Next()) return;
            var a = c.Automaton;
            if (!r.IsNull("unlocked_attachments")) a.LoadUnlocked(r.GetBlobBytes("unlocked_attachments"));
            if (c.MJob != JobPupId && c.SJob != JobPupId) return;

            string name = r.IsNull("name") ? "" : r.Get<string>("name");
            if (name != "") a.Name = name;

            var equip = new byte[14];
            if (!r.IsNull("equipped_attachments"))
            {
                var bytes = r.GetBlobBytes("equipped_attachments");
                Array.Copy(bytes, equip, Math.Min(bytes.Length, equip.Length));
            }
            byte frame = equip[0], head = equip[1];
            if (head < AutomatonInfo.HeadHarlequin || head > AutomatonInfo.HeadSpiritreaver ||
                frame < AutomatonInfo.FrameHarlequin || frame > AutomatonInfo.FrameStormwaker)
            {
                a.Head = head = AutomatonInfo.HeadHarlequin;
                a.Frame = frame = AutomatonInfo.FrameHarlequin;
                Array.Clear(equip, 2, 12);
                for (int i = 0; i < 6; i++) a.ElementMax[i] = 5;
                a.ElementMax[6] = 3;
                a.ElementMax[7] = 3;
                Array.Clear(a.ElementEquip);
            }

            a.SetElementalCapacityBonus((byte)c.GetMod(Mod.AUTO_ELEM_CAPACITY));
            SetHead(c, head);
            SetFrame(c, frame);
            CalculateAutomatonStats(c);

            for (int i = 0; i < 12; i++)
                if (equip[2 + i] == AttachmentOpticFiber || equip[2 + i] == AttachmentOpticFiberII) SetAttachment(c, (byte)i, equip[2 + i]);
            for (int i = 0; i < 12; i++)
                if (equip[2 + i] != AttachmentOpticFiber && equip[2 + i] != AttachmentOpticFiberII) SetAttachment(c, (byte)i, equip[2 + i]);
        }

        private void SaveAutomaton(CharData c)
        {
            if (c.MJob != JobPupId && c.SJob != JobPupId) return;
            Exec("UPDATE char_pet SET equipped_attachments = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.Automaton.EquipBlob()), BoundValue.Of(c.Id));
        }

        // petutils::CalculateAutomatonStats with no automaton out: LoadAutomatonStats' health and stats from
        // xi.pets.automaton.frameStats at the PUP level (capped at 99), the HP / MP boosts of the Auto-Repair Kits and
        // Mana Tanks equipped; a missing table leaves them as they were
        private void CalculateAutomatonStats(CharData c)
        {
            if (lua_ == null) return;
            var a = c.Automaton;
            byte level = (byte)(c.MJob == JobPupId ? c.MLvl + c.GetMod(Mod.AUTOMATON_LVL_BONUS) : c.SLvl);
            int statsLevel = Math.Min((int)level, 99);
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.pets.automaton.frameStats");
                if (!LuaIndex(a.Frame) || !LuaIndex(statsLevel))
                {
                    log_($"zone: LoadAutomatonStats: Missing automaton frame stats for frame {a.Frame} level {statsLevel}");
                    return;
                }
                int Stat(string key) => (int)LuaField(-1, key);
                a.MaxHp = a.Hp = (ushort)Stat("maxHP");
                a.MaxMp = a.Mp = (ushort)Stat("maxMP");

                // a frame without a divisor stops there, before the stats (as C++)
                if (Array.Exists(AutoRepairKits, a.HasAttachment))
                {
                    int tier = BoostTier("xi.automaton.repairKit", "hpBoost", a);
                    if (FrameDivisor("xi.automaton.repairKit", a) is not int divisor || divisor == 0)
                    {
                        log_($"zone: LoadAutomatonStats: Missing Auto-Repair Kit divisor for frame {a.Frame}");
                        return;
                    }
                    a.MaxHp = (ushort)(a.MaxHp + a.MaxHp * tier / divisor);
                    a.Hp = a.MaxHp;
                }
                if (Array.Exists(ManaTanks, a.HasAttachment) && a.MaxMp > 0)
                {
                    int tier = BoostTier("xi.automaton.manaTank", "mpBoost", a);
                    if (FrameDivisor("xi.automaton.manaTank", a) is not int divisor || divisor == 0)
                    {
                        log_($"zone: LoadAutomatonStats: Missing Mana Tank divisor for frame {a.Frame}");
                        return;
                    }
                    a.MaxMp = (ushort)(a.MaxMp + a.MaxMp * tier / divisor);
                    a.Mp = a.MaxMp;
                }

                string[] keys = { "STR", "DEX", "VIT", "AGI", "INT", "MND", "CHR" };
                for (int i = 0; i < keys.Length; i++) a.Stats[i] = (ushort)Stat(keys[i]);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // replaces the table on top with its [key] when that is a table
        private bool LuaIndex(int key)
        {
            if (!lua_.IsTable(-1)) return false;
            lua_.RawGetI(-1, key);
            lua_.Remove(-2);
            return lua_.IsTable(-1);
        }

        // an integral field (the C++ reads these with get<uint16> / get<sol::optional<uint8>>)
        private long LuaField(int idx, string key)
        {
            lua_.GetField(idx, key);
            long v = lua_.IsNumber(-1) ? lua_.ToIntegral(-1) : 0;
            lua_.Pop(1);
            return v;
        }

        // <table>.frameDivisors[frame]
        private int? FrameDivisor(string table, AutomatonInfo a)
        {
            int top = lua_.Top;
            lua_.GetPath(table + ".frameDivisors");
            int? divisor = null;
            if (lua_.IsTable(-1))
            {
                lua_.RawGetI(-1, a.Frame);
                if (lua_.IsNumber(-1)) divisor = (ushort)lua_.ToIntegral(-1);
            }
            lua_.SetTop(top);
            return divisor;
        }

        // the sum of <table>.data's boosts for the attachments equipped (uint8, as C++)
        private int BoostTier(string table, string boostKey, AutomatonInfo a)
        {
            int top = lua_.Top, tier = 0;
            lua_.GetPath(table + ".data");
            if (lua_.IsTable(-1))
            {
                int t = lua_.Top;
                lua_.PushNil();
                while (lua_.Next(t))
                {
                    if (lua_.IsTable(-1) && a.HasAttachment((byte)LuaField(-1, "id"))) tier = (byte)(tier + (int)LuaField(-1, boostKey));
                    lua_.Pop(1);
                }
            }
            lua_.SetTop(top);
            return tier;
        }

        // puppetutils::getSkillCap: xi.pets.automaton.skillCaps' frame rank plus the head's bonus (a frame without the
        // skill counts from 13), as battleutils::GetMaxSkill at the level (capped at 99)
        private int AutomatonSkillCap(CharData c, int skill, int level)
        {
            if (lua_ == null || loader_.Stats is not CharStats stats) return 0;
            int top = lua_.Top, rank = 0;
            try
            {
                lua_.GetPath("xi.pets.automaton.skillCaps");
                if (!lua_.IsTable(-1)) return 0;
                int caps = lua_.Top;
                lua_.GetField(caps, "frames");
                if (!LuaIndex(c.Automaton.Frame)) return 0;
                lua_.RawGetI(-1, skill);
                if (lua_.IsNumber(-1)) rank = (sbyte)lua_.ToIntegral(-1);
                lua_.SetTop(caps);
                lua_.GetField(caps, "heads");
                if (LuaIndex(c.Automaton.Head))
                {
                    lua_.RawGetI(-1, skill);
                    if (lua_.IsNumber(-1)) rank = (sbyte)(rank + (sbyte)lua_.ToIntegral(-1));
                }
            }
            finally
            {
                lua_.SetTop(top);
            }
            if (rank < 0) rank = 13 + rank;
            return stats.Growth.GetMaxSkillByRank(rank, Math.Min(level, 99));
        }

        // ---- 0x044 GP_SERV_COMMAND_EXTENDED_JOB -------------------------------------------------------------------

        // charutils::SendExtendedJobPackets: the main job's packet, then the sub's, for a PUP or BLU; a Monipulator's MON
        // for both
        private void SendExtendedJobPackets(MapSession s, CharData c)
        {
            foreach (var p in ExtendedJobPackets(c)) Push(s, p);
        }

        private List<BasicPacket> ExtendedJobPackets(CharData c)
        {
            var list = new List<BasicPacket>();
            // a Monipulator is MON on both jobs, and retail describes each
            if (c.Monstrosity != null)
            {
                list.Add(ExtendedJobMon(c, true));
                list.Add(ExtendedJobMon(c, false));
                return list;
            }
            foreach (var (job, main) in new[] { (c.MJob, true), (c.SJob, false) })
            {
                if (job == JobPupId) list.Add(ExtendedJobPup(c, main));
                else if (job == JobBluId) list.Add(ExtendedJobBlu(c, main));
            }
            return list;
        }

        // Job @4, IsSubJob @5, Species @8, EquippedInstincts[12] @12
        private static BasicPacket ExtendedJobMon(CharData c, bool main)
        {
            var w = new PacketWriter(0x044, 160);
            w.U8(4, Monstrosity.JobMon);
            w.U8(5, main ? 0 : 1);
            if (c.Monstrosity is { } data)
            {
                w.U16(8, data.Species);
                for (int i = 0; i < data.EquippedInstincts.Length; i++) w.U16(12 + i * 2, data.EquippedInstincts[i]);
            }
            return w.Packet;
        }

        // Job @4, IsSubJob @5, SetSpells[20] @8
        private static BasicPacket ExtendedJobBlu(CharData c, bool main)
        {
            var w = new PacketWriter(0x044, 160);
            w.U8(4, JobBluId);
            w.U8(5, main ? 0 : 1);
            w.Bytes(8, c.SetBlueSpells);
            return w.Packet;
        }

        // Job @4, IsSubJob @5, Head @8, Frame @9, Attachments[12] @10, UnlockedHeads @24, UnlockedFrames @28,
        // UnlockedAttachments @56, Name[16] @88, HP / MaxHP / MP / MaxMP @104, the melee / ranged / magic skill and cap
        // @112, the seven stats and their bonuses @128 (an automaton out: its own HP, MP, stats and their mods; else the
        // bonuses are 0), BonusElementalCapacity @156
        private BasicPacket ExtendedJobPup(CharData c, bool main)
        {
            var a = c.Automaton;
            var w = new PacketWriter(0x044, 160);
            int jobLevel = main ? c.MLvl : c.SLvl;
            w.U8(4, JobPupId);
            w.U8(5, main ? 0 : 1);
            w.U8(8, a.Head);
            w.U8(9, a.Frame);
            w.Bytes(10, a.Attachments);
            w.U32(24, a.UnlockedHeads);
            w.U32(28, a.UnlockedFrames);
            w.Bytes(56, a.UnlockedAttachments);
            w.Name(88, a.Name, 16);
            var pet = c.PPet as AutomatonEntity;
            if (pet != null)
            {
                w.U16(104, pet.Hp == 0 ? pet.MaxHp : pet.Hp);
                w.U16(106, pet.MaxHp);
                w.U16(108, pet.Mp);
                w.U16(110, pet.MaxMp);
            }
            else
            {
                w.U16(104, a.MaxHp);
                w.U16(106, a.MaxHp);
                w.U16(108, a.MaxMp);
                w.U16(110, a.MaxMp);
            }

            int merit = c.Merits?.Value(c, MeritAutomatonSkills) ?? 0;
            int Skill(int skill) => c.WorkingSkills[skill] & 0x7FFF;
            int meleeCap = AutomatonSkillCap(c, SkillAutomatonMelee, jobLevel), meleeBonus = c.GetMod(Mod.AUTO_MELEE_SKILL) + merit;
            int rangedCap = AutomatonSkillCap(c, SkillAutomatonRanged, jobLevel), rangedBonus = c.GetMod(Mod.AUTO_RANGED_SKILL) + merit;
            int magicCap = AutomatonSkillCap(c, SkillAutomatonMagic, jobLevel), magicBonus = c.GetMod(Mod.AUTO_MAGIC_SKILL) + merit;
            w.U16(112, (ushort)meleeBonus + Math.Min((ushort)meleeCap, Skill(SkillAutomatonMelee)));
            w.U16(114, meleeCap + (ushort)meleeBonus);
            w.U16(116, rangedCap == 0 ? rangedBonus : (ushort)rangedBonus + Math.Min(rangedCap, Skill(SkillAutomatonRanged)));
            w.U16(118, rangedCap == 0 ? rangedBonus : rangedCap + (ushort)rangedBonus);
            w.U16(120, magicCap == 0 ? magicBonus : (ushort)magicBonus + Math.Min(magicCap, Skill(SkillAutomatonMagic)));
            w.U16(122, magicCap == 0 ? magicBonus : magicCap + (ushort)magicBonus);

            for (int i = 0; i < 7; i++)
            {
                if (pet == null)
                {
                    w.U16(128 + i * 4, a.Stats[i]);
                    continue;
                }
                w.U16(128 + i * 4, pet.Stats[i]);
                w.U16(130 + i * 4, pet.GetMod((Mod)((int)Mod.STR + i)));
            }
            w.U8(156, c.GetMod(Mod.AUTO_ELEM_CAPACITY));
            return w.Packet;
        }
    }
}
