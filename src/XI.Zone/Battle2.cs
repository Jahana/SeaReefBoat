using System;
using System.Collections.Generic;
using XI.Map;

namespace XI.Zone
{
    // enums/action/category.h, the ones ported
    public static class ActionCategory
    {
        public const byte BasicAttack = 1, SkillFinish = 3, MagicFinish = 4, ItemFinish = 5, SkillStart = 7, MagicStart = 8, ItemStart = 9,
            MobSkillFinish = 11;
        // the job ability categories (CAbility::getActionType; AbilityStart for a cast-time ability's "readies")
        public const byte AbilityFinish = 6, AbilityStart = 10, PetSkillFinish = 13, Dancer = 14, RuneFencer = 15;
    }

    // action_result_t: the additional effect only as a skillchain (recordSkillchain); the reaction (spikes) block only
    // as a counter
    public sealed class ActionResult
    {
        public byte Resolution; // ActionResolution: 0 hit
        public byte Kind;
        public ushort Animation;
        public byte Info, HitDistortion, Knockback;
        public int Param;
        public ushort MessageId;
        public uint Modifier;
        public byte AdditionalEffect, AddEffectInfo; // ActionProcKind (the skillchain's value)
        public int AddEffectParam;
        public ushort AddEffectMessage;
        public byte SpikesEffect, SpikesInfo; // ActionReactKind (63 a counter)
        public int SpikesParam;
        public ushort SpikesMessage;
    }

    // action_target_t
    public sealed class ActionTarget
    {
        public uint ActorId;
        public readonly List<ActionResult> Results = new List<ActionResult>();

        public ActionResult AddResult()
        {
            var r = new ActionResult();
            Results.Add(r);
            return r;
        }
    }

    // action_t
    public sealed class ActionPacket
    {
        public uint ActorId;
        public byte ActionType;
        public uint ActionId;
        public uint RecastSec;
        public readonly List<ActionTarget> Targets = new List<ActionTarget>();

        public ActionTarget AddTarget(uint actorId)
        {
            var t = new ActionTarget { ActorId = actorId };
            Targets.Add(t);
            return t;
        }

        // action_t::normalize for the categories ported: only MagicFinish keeps its recast; a basic attack carries
        // the BasicAttack FourCC; a basic attack's and an item finish's results are kind 1, a weapon skill's and a
        // mob skill's kind 3
        public void Normalize()
        {
            if (ActionType != ActionCategory.MagicFinish) RecastSec = 0;
            if (ActionType == ActionCategory.BasicAttack) ActionId = (uint)FourCC.BasicAttack;
            byte kind = ActionType switch
            {
                ActionCategory.BasicAttack or ActionCategory.ItemFinish => 1,
                ActionCategory.SkillFinish or ActionCategory.MobSkillFinish => 3,
                ActionCategory.AbilityFinish or ActionCategory.Dancer => 2,
                ActionCategory.PetSkillFinish or ActionCategory.RuneFencer => 3,
                _ => 0,
            };
            if (kind != 0)
                foreach (var t in Targets)
                    foreach (var r in t.Results)
                        r.Kind = kind;
        }
    }

    public static class Battle2Packets
    {
        // 0x028 GP_SERV_COMMAND_BATTLE2: the action bit-packed (packBitsBE, low bits first) after the WorkSize byte;
        // at most 15 targets of 8 results each
        public static BasicPacket Battle2(ActionPacket action)
        {
            action.Normalize();
            var p = new BasicPacket();
            p.SetOpcode(0x028);
            var b = p.Buffer;
            int bit = 8 * 5;

            int targets = Math.Min(action.Targets.Count, 15);
            bit = Pack(b, action.ActorId, bit, 32);
            bit = Pack(b, (ulong)targets, bit, 6);
            bit = Pack(b, 0, bit, 4); // res_sum is always 0
            bit = Pack(b, action.ActionType, bit, 4);
            bit = Pack(b, action.ActionId, bit, 32);
            bit = Pack(b, action.RecastSec, bit, 32);

            for (int t = 0; t < targets; t++)
            {
                var target = action.Targets[t];
                int results = Math.Min(target.Results.Count, 8);
                bit = Pack(b, target.ActorId, bit, 32);
                bit = Pack(b, (ulong)results, bit, 4);
                for (int i = 0; i < results; i++)
                {
                    var r = target.Results[i];
                    bit = Pack(b, r.Resolution, bit, 3);
                    bit = Pack(b, r.Kind, bit, 2);
                    bit = Pack(b, r.Animation, bit, 12);
                    bit = Pack(b, r.Info, bit, 5);
                    bit = Pack(b, r.HitDistortion, bit, 2);
                    bit = Pack(b, r.Knockback, bit, 3);
                    bit = Pack(b, (ulong)(long)r.Param, bit, 17);
                    bit = Pack(b, r.MessageId, bit, 10);
                    bit = Pack(b, r.Modifier, bit, 31);
                    bit = Pack(b, r.AdditionalEffect != 0 ? 1UL : 0, bit, 1);
                    if (r.AdditionalEffect != 0)
                    {
                        bit = Pack(b, r.AdditionalEffect, bit, 6);
                        bit = Pack(b, r.AddEffectInfo, bit, 4);
                        bit = Pack(b, (ulong)(long)r.AddEffectParam, bit, 17);
                        bit = Pack(b, r.AddEffectMessage, bit, 10);
                    }
                    bit = Pack(b, r.SpikesEffect != 0 ? 1UL : 0, bit, 1);
                    if (r.SpikesEffect != 0)
                    {
                        bit = Pack(b, r.SpikesEffect, bit, 6);
                        bit = Pack(b, r.SpikesInfo, bit, 4);
                        bit = Pack(b, (ulong)(long)r.SpikesParam, bit, 14);
                        bit = Pack(b, r.SpikesMessage, bit, 10);
                    }
                }
            }

            int workSize = (bit >> 3) + (bit % 8 != 0 ? 1 : 0);
            b[4] = (byte)workSize;
            p.SetSize((uint)workSize + 1);
            return p;
        }

        // packBitsBE: the value's bit i lands at bit (offset + i), counting each byte from its low bit
        public static int Pack(byte[] target, ulong value, int bitOffset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                int at = bitOffset + i;
                if ((value >> i & 1) != 0) target[at >> 3] |= (byte)(1 << (at & 7));
                else target[at >> 3] &= (byte)~(1 << (at & 7));
            }
            return bitOffset + length;
        }

        public static ulong Unpack(byte[] source, int bitOffset, int length)
        {
            ulong v = 0;
            for (int i = 0; i < length; i++)
            {
                int at = bitOffset + i;
                if ((source[at >> 3] >> (at & 7) & 1) != 0) v |= 1UL << i;
            }
            return v;
        }

        // ActionInterrupts::ItemInterrupt: an item start that stops, aimed at the char itself with one empty result
        public static BasicPacket ItemInterrupt(CharData c)
        {
            var action = new ActionPacket { ActorId = c.Id, ActionType = ActionCategory.ItemStart, ActionId = (uint)FourCC.ItemInterrupt };
            action.AddTarget(c.Id).AddResult();
            return Battle2(action);
        }
    }
}
