using System;
using System.Collections.Generic;
using System.Linq;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // GP_CLI_COMMAND_MYROOM_JOB (0x100): the job change at a moogle. The gear sets are rechecked (CheckForGearSet), the
    // ability recasts go (PRecastContainer->ChangeJob), the Teleport effect's power is zeroed, the dispelable / roll /
    // job change effects go and an item use or a spell being cast is interrupted.
    public sealed partial class ZoneHost
    {
        private const byte JobMon = 23;
        private static readonly Container[] JobChangeGearContainers =
        {
            Container.Inventory, Container.Wardrobe, Container.Wardrobe2, Container.Wardrobe3, Container.Wardrobe4,
            Container.Wardrobe5, Container.Wardrobe6, Container.Wardrobe7, Container.Wardrobe8,
        };

        // MainJobIndex u8 @4, SupportJobIndex u8 @5 (0 keeps that job)
        private void RegisterMyroomJobHandler()
        {
            Register(0x100, (s, c, p) => ValidateMyroomJob(c, p.Buffer[4], p.Buffer[5]),
                     (s, c, p) => OnMyroomJob(s, c, p.Buffer[4], p.Buffer[5]));
        }

        private PacketValidator ValidateMyroomJob(CharData c, byte main, byte support)
        {
            var pv = Pv(c)
                .BlockedBy(BlockedState.InEvent | BlockedState.Monstrosity)
                .MustEqual(AreaOf(c).CanUseMisc("mogmenu") || c.MoghouseId == c.Id ? 1 : 0, 1, "Player not in MH or zone with Moogle.");
            if (main != 0)
                pv.Range("MainJobIndex", main, 0x01, CharData.MaxJobs - 1)
                  .MustEqual((c.UnlockedJobs & 1u << main) != 0 ? 1 : 0, 1, "Main job not unlocked")
                  .MustEqual(main != JobMon ? 1 : 0, 1, "MON is only entered through the Feretory");
            if (support != 0)
                pv.Range("SupportJobIndex", support, 0x00, CharData.MaxJobs - 1)
                  .MustEqual((c.UnlockedJobs & 1u << support) != 0 ? 1 : 0, 1, "Support job not unlocked")
                  .MustEqual(support != JobMon ? 1 : 0, 1, "MON is never a support job");
            return pv;
        }

        private void OnMyroomJob(MapSession s, CharData c, byte main, byte support)
        {
            var stats = loader_.Stats;
            if (stats == null)
            {
                log_($"zone: job change from {c.Name} needs the stat tables");
                return;
            }
            int ratio = stats.Settings.SubjobRatio;

            bool hadBlueMage = c.MJob == JobBluId || c.SJob == JobBluId;
            if (main > 0)
            {
                ResetPetZoningInfo(c);
                byte prevMain = c.MJob;
                SaveJobChangeGear(c);
                stats.RemoveAllEquipment(c);
                c.MJob = main;
                // the new main matching the sub swaps them, as retail
                if (c.SJob == c.MJob) c.SJob = prevMain;
                SetMLevel(c, c.JobLevels[c.MJob]);
                SetSLevel(c, c.JobLevels[c.SJob], ratio);
                LoadAutomaton(c);

                bool canUseMeritMode = c.JobLevels[c.MJob] >= 75 && c.HasKeyItem(SelfPackets.KeyItemLimitBreaker);
                if (!canUseMeritMode && c.MeritMode && Exec("UPDATE char_exp SET mode = ? WHERE charid = ? LIMIT 1", BoundValue.Of(0), BoundValue.Of(c.Id)) != null)
                    c.MeritMode = false;
            }

            // a sub change to the current main is refused
            if (support > 0 && support != c.MJob)
            {
                ResetPetZoningInfo(c);
                c.SJob = support;
                SetSLevel(c, c.JobLevels[c.SJob], ratio);
                LoadAutomaton(c);

                // only a shield or a hand-to-hand weapon stays in the sub slot
                uint subType = c.Equipped[CharStats.SlotSub]?.Weapon?.DmgType ?? 0;
                if (subType > DamageTypeNone && subType < DamageTypeHandToHand) stats.UnequipItem(c, CharStats.SlotSub);
            }

            // the blue magic for the jobs now held
            bool hasBlueMage = c.MJob == JobBluId || c.SJob == JobBluId;
            if (hasBlueMage && !hadBlueMage) LoadSetSpells(c);
            else if (hasBlueMage) ValidateBlueSpells(c);
            else if (hadBlueMage) UnequipAllBlueSpells(s, c);

            SetStyleLock(s, c, false);
            CheckForGearSet(c);

            if (loader_.RefreshGiftMods(c)) Push(s, LoginPackets.MagicData(c));
            stats.BuildSkillsTable(c);
            loader_.CalculateStats(c);
            BuildCharTraits(c);
            (c.Recasts as CharRecastContainer)?.ChangeJob();
            loader_.BuildAbilityTable(c);
            stats.BuildWeaponSkills(c);
            LoadJobChangeGear(c, stats);

            // a pending teleport or warp is cancelled, and the job's buffs go
            if (c.StatusEffects.GetStatusEffect(EffectId.Teleport) is StatusEffect teleport) teleport.Power = 0;
            c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Dispelable | EffectFlag.Roll | EffectFlag.OnJobChange);
            // InterruptStates: an item being used or a spell being cast is interrupted
            CancelItemUse(s, c);
            AbortMagic(c, NowMs());
            // the party's JobInParty latents
            LatentsForParty(c, m => m.Latents.CheckLatentsPartyJobs());

            CharStats.UpdateHealth(c);
            c.Hp = c.MaxHp;
            c.Mp = c.MaxMp;
            c.UpdateMask |= CharStats.UpdateHp;
            // the style lock flag goes out with the appearance
            c.EquipDirty = true;

            SaveCharStats(c);

            Push(s, GameOkPackets.JobInfo(c, UnixNow()));
            Push(s, CharStatus(c));
            Push(s, SelfPackets.CliStatus(c, stats, UnityStatus(c)));
            Push(s, SelfPackets.CliStatus2(c));
            Push(s, AbilRecast(c));
            Push(s, LoginPackets.CommandData(c));
            SendExtendedJobPackets(s, c);
            Push(s, SelfPackets.Merits(c, stats));
            Push(s, MonPage1(c));
            Push(s, GameOkPackets.Monstrosity2(c));
            Push(s, CharSyncOf(c));
        }

        private const uint DamageTypeNone = 0, DamageTypeHandToHand = 4;

        // CBattleEntity::SetMLevel for a char: 0 counts as 1, saved
        private void SetMLevel(CharData c, byte level)
        {
            c.MLvl = level == 0 ? (byte)1 : level;
            Exec("UPDATE char_stats SET mlvl = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.MLvl), BoundValue.Of(c.Id));
        }

        // CBattleEntity::SetSLevel for a char: capped by map.SUBJOB_RATIO, saved
        private void SetSLevel(CharData c, byte level, int ratio)
        {
            // a Monipulator's sub job is MON at its main level
            c.SLvl = c.Monstrosity != null ? c.MLvl : CharLoader.SubLevel(c.MLvl, level, ratio);
            Exec("UPDATE char_stats SET slvl = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.SLvl), BoundValue.Of(c.Id));
        }

        private static readonly string[] EquipSavedColumns =
        {
            "main", "sub", "ranged", "ammo", "head", "body", "hands", "legs", "feet", "neck", "waist", "ear1", "ear2", "ring1", "ring2", "back",
        };

        // charutils::SaveJobChangeGear: the item ids in the 16 equipment slots, kept for the job being left
        private void SaveJobChangeGear(CharData c)
        {
            var values = new List<BoundValue> { BoundValue.Of(c.Id), BoundValue.Of(c.MJob) };
            for (int slot = 0; slot < EquipSavedColumns.Length; slot++)
                values.Add(BoundValue.Of(CharStats.EquippedItem(c, slot)?.ItemId ?? (ushort)0));
            Exec("INSERT INTO char_equip_saved SET charid = ?, jobid = ?, " + string.Join(", ", EquipSavedColumns.Select(col => col + " = ?")) +
                 " ON DUPLICATE KEY UPDATE " + string.Join(", ", EquipSavedColumns.Select(col => $"{col} = VALUES({col})")),
                 values.ToArray());
        }

        // charutils::LoadJobChangeGear: the new main job's saved gear, each id equipped from the first of the inventory
        // and wardrobes holding a piece of equipment with it (in slot order), never the item the paired slot already has
        private void LoadJobChangeGear(CharData c, CharStats stats)
        {
            var r = Exec("SELECT main, sub, ranged, ammo, head, body, hands, legs, feet, neck, waist, ear1, ear2, ring1, ring2, back " +
                          "FROM char_equip_saved AS equip WHERE charid = ? AND jobid = ? LIMIT 1", BoundValue.Of(c.Id), BoundValue.Of(c.MJob));
            if (r == null || !r.Next()) return;
            var ids = EquipSavedColumns.Select(col => r.Get<ushort>(col)).ToArray();
            for (int equipSlot = CharStats.SlotMain; equipSlot <= CharStats.SlotBack; equipSlot++)
            {
                ushort itemId = ids[equipSlot];
                if (itemId == 0) continue;

                Item compareItem = equipSlot switch
                {
                    CharStats.SlotMain or CharStats.SlotEar1 or CharStats.SlotRing1 => CharStats.EquippedItem(c, equipSlot + 1),
                    CharStats.SlotSub or CharStats.SlotEar2 or CharStats.SlotRing2 => CharStats.EquippedItem(c, equipSlot - 1),
                    _ => null,
                };

                foreach (var container in JobChangeGearContainers)
                {
                    var equip = c.Items.Where(i => i.Location == container && i.ItemId == itemId && i != compareItem &&
                                                   stats.Items.Equipment(i.ItemId) != null)
                                       .OrderBy(i => i.Slot).FirstOrDefault();
                    if (equip == null) continue;
                    stats.EquipItem(c, equip.Slot, (byte)equipSlot, container);
                    break;
                }
            }
        }
    }
}
