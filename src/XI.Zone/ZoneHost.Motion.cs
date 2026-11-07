using System;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // The C2S handlers for what the char does with itself where others can see it, and the packets with nothing to do
    // (c2s/0x00d, 0x01c, 0x05d, 0x0dc, 0x0ea, 0x0eb, 0x0f2, 0x113, 0x119, 0x11d). A sit reaches the other chars through
    // the updatemask's 0x00D (SendUpdateMask); the emote and the jump go to them directly.
    public sealed partial class ZoneHost
    {
        // Emote / EmoteMode (enums/emote.h)
        private const byte EmoteSalute = 2, EmoteHurray = 43, EmoteBell = 73, EmoteJob = 74, EmoteAim = 96;
        private const byte EmoteModeMotion = 2;
        private const byte AnimationSit = 47, AnimationSitchair0 = 63;
        private const ushort MsgCharacterInfoHidden = 175, MsgCharacterInfoShown = 176, MsgHeadgearShow = 260, MsgHeadgearHide = 261;

        // the bells /bell plays on: Dream Bell, Lady Bell and their +1s (not the geomancer bells)
        private static readonly ushort[] ValidBells = { 18863, 18864, 18868, 18869 };

        // GP_SERV_COMMAND_EMOTE_LIST: the job gesture key items WAR..RUN, then the chair key items
        private static readonly int[] JobGestureKeyItems =
        {
            1738, 1739, 1740, 1741, 1742, 1743, 1744, 1745, 1746, 1747, 1748, 1749, 1750, 1751, 1752, 1753, 1754, 1755, 1756, 1757,
            2963, 2964,
        };
        private const int ImperialChairKeyItem = 2826, ChairKeyItems = 11;

        private static PacketValidator ValidateMotion(CharData c, BasicPacket p) => Pv(c)
            .BlockedBy(BlockedState.InEvent)
            .OneOf("Mode", p.Buffer[11], 0, 1, 2)
            .Range("Number", p.Buffer[10], 0, EmoteAim);

        private static PacketValidator ValidateSit(CharData c, uint mode) => Pv(c)
            .BlockedBy(BlockedState.InEvent | BlockedState.Dead | BlockedState.AbnormalStatus | BlockedState.Crafting | BlockedState.PreventAction)
            .OneOf("Mode", mode, 0, 1, 2);

        // GP_CLI_COMMAND_MOTION: the emote, to the char and everyone in range, then xi.player.onPlayerEmote
        private void OnMotion(MapSession s, CharData c, BasicPacket p)
        {
            if (InPrison(c))
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgCannotUseInArea));
                return;
            }
            uint targetId = U32(p, 4);
            ushort targetIndex = U16(p, 8);
            byte number = p.Buffer[10], mode = p.Buffer[11];
            ushort param = U16(p, 12);

            if (number == EmoteBell)
            {
                // a bell in the lockstyle is enough whatever is held, but any other style over a held bell is not
                ushort main = c.StyleItems[CharStats.SlotMain];
                if (main == 0) main = c.Equipped[CharStats.SlotMain]?.ItemId ?? 0;
                if (Array.IndexOf(ValidBells, main) < 0) return;
                if (param < 0x06 || param > 0x1E) return; // not a note
            }
            else if (number == EmoteJob && param != 0)
            {
                int jobIndex = param - 0x1E;
                if (param < 0x1E || jobIndex >= CharData.MaxJobs || (c.UnlockedJobs & 1u << jobIndex) == 0) return;
            }

            PushCharInRange(c, MotionMes(c, targetId, targetIndex, number, mode, param), self: true);
            CallPlayerGlobal("xi.player.onPlayerEmote", c, number);
        }

        // GP_SERV_COMMAND_MOTIONMES: Param is the nation for /salute, the weapon for /hurray and /aim, the note for /bell
        // and the job for /jobemote
        private static BasicPacket MotionMes(CharData c, uint targetId, ushort targetIndex, byte emote, byte mode, ushort extra)
        {
            var w = new PacketWriter(0x05A, 56);
            w.U32(4, c.Id);
            w.U32(8, targetId);
            w.U16(12, c.TargId);
            w.U16(14, targetIndex);
            w.U16(16, emote == EmoteJob ? emote + (extra - 0x1F) : emote);
            ushort param = 0;
            if (emote == EmoteSalute) param = c.Nation;
            else if (emote == EmoteHurray)
            {
                if (c.Equipped[CharStats.SlotMain] is EquipmentInfo weapon && weapon.ItemId != 65535) param = weapon.ItemId;
            }
            else if (emote == EmoteAim)
            {
                param = 65535;
                if (c.Equipped[CharStats.SlotRanged] is EquipmentInfo ranged && ranged.ItemId != 65535)
                {
                    int skill = ranged.Weapon?.Skill ?? Skill.None;
                    if (skill == Skill.Throwing) param = ranged.ItemId;
                    else if ((skill == Skill.Marksmanship || skill == Skill.Archery) &&
                             c.Equipped[CharStats.SlotAmmo] is EquipmentInfo ammo && ammo.ItemId != 65535)
                        param = ranged.ItemId;
                }
            }
            else if (emote == EmoteBell)
            {
                mode = EmoteModeMotion; // /bell has no emote text
                param = (ushort)(extra - 0x06);
            }
            else if (emote == EmoteJob) param = (ushort)(extra - 0x1F);
            w.U16(18, param);
            w.U8(22, mode);
            return w.Packet;
        }

        // GP_CLI_COMMAND_JUMP: /jump, to the char and everyone in range
        private void OnJump(MapSession s, CharData c, BasicPacket p)
        {
            if (InPrison(c))
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgCannotUseInArea));
                return;
            }
            var w = new PacketWriter(0x11E, 8);
            w.U16(4, U16(p, 8));
            PushCharInRange(c, w.Packet, self: true);
        }

        // HEALING lost silently (its onEffectLose drops a pending logout): a /sit or /sitchair may cut into resting
        private static void DropHealing(CharData c) => c.StatusEffects.DelStatusEffectSilent(EffectId.Healing);

        // GP_CLI_COMMAND_SIT: Toggle / On / Off; a wyvern or automaton sits along
        private void OnSit(MapSession s, CharData c, uint mode)
        {
            DropHealing(c);
            c.Animation = mode switch
            {
                0 => c.Animation == AnimationSit ? AnimationNone : AnimationSit,
                1 => AnimationSit,
                _ => AnimationNone,
            };
            c.UpdateMask |= CharStats.UpdateHp;
            if (c.PPet is PetEntity { PetType: PetType.Wyvern or PetType.Automaton } pet)
            {
                pet.Animation = c.Animation;
                pet.UpdateMask |= EntityUpdatePacket.UpdateHp;
            }
        }

        // GP_CLI_COMMAND_SITCHAIR: chair 0 is anyone's, the others need their key item; the same chair again stands up
        private void OnSitChair(MapSession s, CharData c, uint mode, uint chair)
        {
            DropHealing(c);
            if (mode == 2)
            {
                c.Animation = AnimationNone;
                c.UpdateMask |= CharStats.UpdateHp;
                return;
            }
            byte chairId = (byte)(chair + AnimationSitchair0);
            if (chairId != AnimationSitchair0 && !c.HasKeyItem(chairId + 0xACA)) chairId = AnimationSitchair0;
            c.Animation = c.Animation == chairId ? AnimationNone : chairId;
            c.UpdateMask |= CharStats.UpdateHp;
        }

        // GP_SERV_COMMAND_EMOTE_LIST
        private static BasicPacket EmoteList(CharData c)
        {
            var w = new PacketWriter(0x11A, 12);
            uint jobs = 0;
            for (int i = 0; i < JobGestureKeyItems.Length; i++)
                if (c.HasKeyItem(JobGestureKeyItems[i])) jobs |= 1u << i;
            w.U32(4, jobs);
            int chairs = 0;
            for (int i = 0; i < ChairKeyItems; i++)
                if (c.HasKeyItem(ImperialChairKeyItem + i)) chairs |= 1 << i;
            w.U16(8, (ushort)chairs);
            return w.Packet;
        }

        // GP_CLI_COMMAND_REQSUBMAPNUM: only answered while an event or sequence holds the char (the C++ doubts it too)
        private void OnReqSubMapNum(MapSession s, CharData c)
        {
            if (!c.IsNpcLocked) return;
            var w = new PacketWriter(0x10E, 8);
            w.U32(4, 0);
            Push(s, w.Packet);
        }

        // GP_CLI_COMMAND_SUBMAPCHANGE: the sub-region the client walked into
        private void OnSubMapChange(MapSession s, CharData c, BasicPacket p)
        {
            c.Boundary = U16(p, 6);
            // SaveCharPosition skips a Disappear (zoning) char
            if (!zoneChanges_.ContainsKey(s)) SavePosition(c);
        }

        // GP_CLI_COMMAND_CONFIG: each flag set in the packet goes to SetFlg (1 on, 2 off); seeking a party and
        // recruiting exclude each other, mentor needs unlocking, and the new adventurer mark only comes off
        private void OnConfig(MapSession s, CharData c, BasicPacket p)
        {
            bool value = p.Buffer[16] == 1;
            uint flags = U32(p, 4);
            bool updated = false;

            bool Requested(int bit) => (flags & 1u << bit) != 0;
            bool Has(int bit) => (c.Settings & 1u << bit) != 0;
            void Set(int bit, bool on)
            {
                if (on) c.Settings |= 1u << bit;
                else c.Settings &= ~(1u << bit);
            }

            const int Invite = 0, Away = 1, Anonymity = 2, AutoTargetOff = 14, AutoParty = 15, Mentor = 25, NewAdventurerOff = 26,
                DisplayHeadOff = 27, Recruit = 29;

            if (Requested(Invite) && Has(Invite) != value)
            {
                updated = true;
                Set(Invite, value);
                if (value) Set(Recruit, false);
            }
            if (Requested(Away) && Has(Away) != value)
            {
                updated = true;
                Set(Away, value);
            }
            if (Requested(Anonymity) && Has(Anonymity) != value)
            {
                updated = true;
                Set(Anonymity, value);
                Push(s, SystemMessage(value ? MsgCharacterInfoHidden : MsgCharacterInfoShown));
            }
            if (Requested(AutoTargetOff) && Has(AutoTargetOff) != value)
            {
                updated = true;
                Set(AutoTargetOff, value);
            }
            if (Requested(AutoParty) && Has(AutoParty) != value)
            {
                updated = true;
                Set(AutoParty, value);
            }
            if (Requested(Mentor) && Has(Mentor) != value && c.MentorUnlocked)
            {
                updated = true;
                Set(Mentor, value);
            }
            if (Requested(NewAdventurerOff) && Has(NewAdventurerOff) != value && !Has(NewAdventurerOff))
            {
                updated = true;
                Set(NewAdventurerOff, value);
            }
            if (Requested(DisplayHeadOff) && Has(DisplayHeadOff) != value)
            {
                updated = true;
                Set(DisplayHeadOff, value);
                // only blink with headgear on
                if (c.Equipped[StyleLock.SlotHead] != null) Push(s, LoginPackets.GrapList(c));
                Push(s, SelfPackets.Message(value ? MsgHeadgearHide : MsgHeadgearShow));
            }
            if (Requested(Recruit) && Has(Recruit) != value)
            {
                updated = true;
                Set(Recruit, value);
                if (value) Set(Invite, false);
            }

            // retail answers even when nothing changed
            Push(s, GameOkPackets.Config(c));
            Push(s, CharStatus(c));
            if (updated)
            {
                Push(s, CharSyncOf(c));
                c.UpdateMask |= CharStats.UpdateHp;
                SaveCharStats(c);
                Exec("UPDATE chars SET settings = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.Settings), BoundValue.Of(c.Id));
            }
        }

        // charutils::SaveCharStats: the HP, MP, mog house flag and jobs (the sub job as set: GetSJob(true)), and the pet
        // zoning columns (petZoningInfo)
        private void SaveCharStats(CharData c)
        {
            var z = c.PetZoning;
            Exec("UPDATE char_stats SET hp = ?, mp = ?, mhflag = ?, mjob = ?, sjob = ?, pet_id = ?, pet_type = ?, pet_hp = ?, pet_mp = ?, pet_level = ? " +
                 "WHERE charid = ?",
                 BoundValue.Of(c.Hp), BoundValue.Of(c.Mp), BoundValue.Of(c.MhFlag), BoundValue.Of(c.MJob), BoundValue.Of(c.SJobRaw),
                 BoundValue.Of((ushort)z.PetId), BoundValue.Of((byte)z.PetType), BoundValue.Of((ushort)z.PetHp), BoundValue.Of((ushort)z.PetMp),
                 BoundValue.Of(z.PetLevel), BoundValue.Of(c.Id));
            // the jug pet's state rides in char vars
            SaveJugPetCharVars(c);
        }

        // luautils::callGlobal<void>(path, player, args...)
        private void CallPlayerGlobal(string path, CharData c, params double[] args)
        {
            if (lua_ == null) return;
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return;
                }
                lua_.PushObject(c);
                foreach (double a in args) lua_.Push(a);
                lua_.Call(1 + args.Length, 0);
            }
            catch (LuaException e)
            {
                ScriptError(path, e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }
    }
}
