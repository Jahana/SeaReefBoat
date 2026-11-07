using System;
using System.Collections.Generic;
using XI.Map;

namespace XI.Zone
{
    // Raise: the raise level a dead char holds (m_hasRaise, from sendRaise / sendReraise or the reraise mods), the
    // death state's raise menu (GP_SERV_COMMAND_RES 8 s into the death), the menu's answer (ACTIONID::RaiseMenu) and
    // CCharEntity::OnRaise (weakness, the HP back, the raise animation, part of the EXP lost); tractor (sendTractor's
    // offer and ACTIONID::TractorMenu's answer: the char rezoned at the caster's position). Mijin Gakure's death (the
    // death state's mijin param) raises without weakness and with half the HP. Not ported: the Moghancement EXP TODO
    // of the C++.
    public sealed partial class ZoneHost
    {
        private const long RaiseMenuDelayMs = 8000, RaisePromptAgainMs = 12000, RaiseStandUpMs = 2000;
        private const long WeaknessDurationMs = 5 * 60_000, AriseWeaknessDurationMs = 3 * 60_000, AriseReraiseDurationMs = 3600_000;
        private const ushort EffectReraise = 113;
        private const byte ActionCategoryMagicFinish = 4;
        // ActionAnimation
        private const ushort AnimRaise = 511, AnimRaise2 = 512, AnimRaise3 = 496, AnimArise = 847;
        // GP_SERV_COMMAND_RES_TYPE
        private const ushort ResHomepoint = 0, ResRaise = 1, ResTractor = 2;

        // GP_SERV_COMMAND_RES (0x0F9): the char's homepoint menu turned to raise (or tractor)
        private static BasicPacket ResPacket(CharData c, ushort type)
        {
            var w = new PacketWriter(0x0F9, 12);
            w.U32(4, c.Id);
            w.U16(8, c.TargId);
            w.U16(10, type);
            return w.Packet;
        }

        // CCharEntity::Die(duration)'s reraise mods: the best one held, Mijin Gakure's when there is none
        private static void ReraiseFromMods(CharData c)
        {
            if (c.GetMod(Mod.RERAISE_I) > 0) c.HasRaise = 1;
            if (c.GetMod(Mod.RERAISE_II) > 0) c.HasRaise = 2;
            if (c.GetMod(Mod.RERAISE_III) > 0) c.HasRaise = 3;
            if (c.HasRaise == 0 && c.GetMod(Mod.MIJIN_RERAISE) > 0) c.HasRaise = 1;
        }

        // CDeathState::Update for a char: the raise menu once the char has a raise, 8 s in (or 12 s after
        // allowSendRaisePrompt); stood up 2 s after accepting
        private void TickRaise(CharData c, long now)
        {
            if (c.Hp > 0)
            {
                if (c.RaiseAcceptedAtMs != 0 && now > c.RaiseAcceptedAtMs + RaiseStandUpMs)
                {
                    c.RaiseAcceptedAtMs = 0;
                    c.Animation = Anim.None;
                    c.UpdateMask |= CharStats.UpdateHp;
                    if (SessionOf(c) is { } s) SendUpdateMask(s, c);
                }
                return;
            }
            if (now > c.RaiseMenuAtMs && !c.RaiseSent && c.HasRaise != 0)
            {
                PushTo(c, ResPacket(c, ResRaise));
                c.RaiseSent = true;
            }
        }

        // ACTIONID::RaiseMenu: accepting raises, anything else gives the raise up
        private void OnRaiseMenu(CharData c, HomepointMenuStatus status, long now)
        {
            if (c.HasRaise == 0) return;
            if (status == HomepointMenuStatus.Accept) Raise(c, now);
            else c.HasRaise = 0;
        }

        // ACTIONID::TractorMenu: accepting rezones the char where the tractor pulls to (status Disappear, the boundary
        // and the packets dropped); the offer is gone either way
        private void OnTractorMenu(MapSession s, CharData c, TractorMenuStatus status)
        {
            if (status == TractorMenuStatus.Accept && c.HasTractor != 0)
            {
                (c.X, c.Y, c.Z, c.Rotation) = (c.TractorX, c.TractorY, c.TractorZ, c.TractorRotation);
                c.Boundary = 0;
                s.PacketList.Clear();
                zoneChanges_[s] = c.Zone;
            }
            c.HasTractor = 0;
        }

        // CCharEntity::Raise: OnRaise, then the death state ends (CDeathState::acceptRaise) and the death time clears
        private void Raise(CharData c, long now)
        {
            bool inDeathState = c.DeathAtMs != 0;
            OnRaise(c);
            if (inDeathState) c.RaiseAcceptedAtMs = now;
            c.DeathAtMs = 0;
        }

        // CCharEntity::OnRaise: weakness (its level climbing with each raise in a row; 3 minutes after Arise; none after
        // Mijin Gakure), the HP and the share of the EXP lost the raise level returns (Mijin Gakure: half the HP, all
        // of it under MIJIN_RERAISE, no EXP), Arise's Reraise III
        private void OnRaise(CharData c)
        {
            if (c.HasRaise == 0) return;
            if (c.WeaknessLvl == 0) c.WeaknessLvl = 1;
            // dynamic_cast<CDeathState*>(GetCurrentState()): the death state's params while the char is in it
            bool mijin = c.DeathAtMs != 0 && c.DeathState.Mijin;
            if (!mijin)
            {
                c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.Weakness, EffectId.Weakness, c.WeaknessLvl, 0,
                                                                 c.HasArise ? AriseWeaknessDurationMs : WeaknessDurationMs));
            }

            double retain = 1 - (loader_.Stats?.Settings.ExpRetain ?? 0);
            double ratioReturned = 0.0f;
            ushort hpReturned = 1;
            var action = new ActionPacket { ActorId = c.Id, ActionType = ActionCategoryMagicFinish };
            var result = action.AddTarget(c.Id).AddResult();
            // Mijin Gakure used with MIJIN_RERAISE MOD
            if (mijin && c.GetMod(Mod.MIJIN_RERAISE) != 0)
            {
                result.Animation = AnimRaise;
                hpReturned = (ushort)c.MaxHp;
            }
            else switch (c.HasRaise)
            {
                case 1:
                    result.Animation = AnimRaise;
                    hpReturned = (ushort)(mijin ? c.MaxHp * 0.5 : c.MaxHp * 0.1);
                    ratioReturned = 0.50f * retain;
                    break;
                case 2:
                    result.Animation = AnimRaise2;
                    hpReturned = (ushort)(mijin ? c.MaxHp * 0.5 : c.MaxHp * 0.25);
                    ratioReturned = (c.MLvl <= 50 ? 0.50f : 0.75f) * retain;
                    break;
                case 3:
                    result.Animation = AnimRaise3;
                    hpReturned = (ushort)(c.MaxHp * 0.5);
                    ratioReturned = (c.MLvl <= 50 ? 0.50f : 0.90f) * retain;
                    break;
                case 4: // Arise, and Reraise IV's
                    result.Animation = AnimArise;
                    hpReturned = (ushort)c.MaxHp;
                    ratioReturned = (c.MLvl <= 50 ? 0.50f : 0.90f) * retain;
                    break;
            }
            AddHp(c, Math.Max(hpReturned, (ushort)1));
            c.UpdateMask |= CharStats.UpdateHp;
            PushCharInRange(c, Battle2Packets.Battle2(action), true);

            ushort expLost = (ushort)CharVar(c, "expLost");
            if (expLost != 0)
            {
                var xpReturned = (ushort)Math.Ceiling(expLost * ratioReturned);
                AddExperiencePoints(false, c, c, xpReturned, MobTooWeak, false, true, expFromRaise: true);
                SetCharVar(c, "expLost", 0, 0);
            }

            if (c.HasArise) c.StatusEffects.AddStatusEffect(new StatusEffect(EffectReraise, EffectReraise, 3, 0, AriseReraiseDurationMs));
            c.HasArise = false;
            c.HasRaise = 0;
        }

        private void RegisterRaiseBindings(Dictionary<string, XI.Script.LuaFunction> m)
        {
            // sendRaise: a raise spell's level (4 Arise), its menu at once; refused while one is held
            m["sendRaise"] = l =>
            {
                byte raiseLevel = (byte)l.ToUIntArg(2);
                if (Pc(l, "sendRaise") is not CharData c) return 0;
                if (raiseLevel == 0 || raiseLevel > 4) log_("lua::sendRaise raise value is not valid!");
                else if (c.HasTractor == 0 && c.HasRaise == 0)
                {
                    c.HasRaise = raiseLevel;
                    if (raiseLevel == 4) c.HasArise = true;
                    PushTo(c, ResPacket(c, ResRaise));
                }
                return 0;
            };
            // sendTractor(x, y, z, rotation): the tractor menu, where it pulls to kept; refused while a raise or tractor is
            // held
            m["sendTractor"] = l =>
            {
                if (Pc(l, "sendTractor") is not CharData c) return 0;
                if (c.HasTractor == 0 && c.HasRaise == 0)
                {
                    c.HasTractor = 1;
                    c.TractorX = (float)l.ToNumberArg(2);
                    c.TractorY = (float)l.ToNumberArg(3);
                    c.TractorZ = (float)l.ToNumberArg(4);
                    c.TractorRotation = (byte)l.ToIntArg(5);
                    PushTo(c, ResPacket(c, ResTractor));
                }
                return 0;
            };
            // sendReraise: the reraise effect wearing off a dead char; the death state sends the menu
            m["sendReraise"] = l =>
            {
                byte raiseLevel = (byte)l.ToUIntArg(2);
                if (Pc(l, "sendReraise") is not CharData c) return 0;
                if (raiseLevel == 0 || raiseLevel > 4) log_("lua::sendRaise raise value is not valide!");
                else if (c.HasRaise == 0) c.HasRaise = raiseLevel;
                return 0;
            };
            // allowSendRaisePrompt: the menu again 12 s on (a dead char moved loses it)
            m["allowSendRaisePrompt"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("m_PBaseEntity is null or not a Player.");
                    return 0;
                }
                if (c.DeathAtMs != 0)
                {
                    c.RaiseMenuAtMs = NowMs() + RaisePromptAgainMs;
                    c.RaiseSent = false;
                }
                return 0;
            };
        }
    }
}
