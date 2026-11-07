using System;
using System.Collections.Generic;
using XI.Map;
using XI.Script;

namespace XI.Zone
{
    // A char's latent effects (CLatentEffectContainer, CLatentEffect and the CLuaBaseEntity addLatent, delLatent and
    // hasAllLatentsActive bindings): what the container reads outside the char, the party-wide recheck (ForParty) and
    // the Vana'diel hour's (CTimeServer: the hour, the moon, the day of the week and the time of day). The other checks
    // are called where the C++ calls them: equip / unequip (charutils::EquipItem / UnequipItem, the level-sync
    // RemoveAllEquipMods / ApplyAllEquipMods and the 0x050 / 0x051 retrigger), addHP / addMP / addTP, the status effect
    // gain and loss, engage / disengage / a new target, the party changes, a job change and a level up, and zoning in.
    // The weather change's CheckLatentsWeather is ZoneHost.Weather's (CZoneEntities::WeatherChange).
    // The conquest update's CheckLatentsZone is ZoneHost.Conquest's. The weapon skill's CheckLatentsTP /
    // CheckLatentsWS are the weapon skill code's.
    public sealed partial class ZoneHost : ILatentHost
    {
        // ---- ILatentHost ---------------------------------------------------------------------------------------------

        CharStats ILatentHost.LatentStats => loader_?.Stats;

        void ILatentHost.LatentPush(CharData c, BasicPacket p) => PushTo(c, p);

        long ILatentHost.LatentVanaTicks() => VanaTicksNow();

        ushort ILatentHost.LatentWeather(CharData c, ushort? weather) => Weather(c, false, weather ?? ZoneInfoOf(c.Zone).Weather);

        bool ILatentHost.LatentZoneHasType(ushort zone, string type) => ZoneInfoOf(zone).Types.Contains(type);

        // conquest::GetRegionOwner
        byte ILatentHost.LatentRegionOwner(byte region) => GetConquestData().RegionOwner(region);

        // CBattleEntity::isInGarrison: luautils::callGlobal<bool>("xi.garrison.isInGarrison", this), false on any failure
        bool ILatentHost.LatentInGarrison(CharData c)
        {
            if (lua_ == null) return false;
            const string path = "xi.garrison.isInGarrison";
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return false;
                }
                lua_.PushObject(c);
                lua_.Call(1, 1);
                if (lua_.Type(-1) != 1)
                {
                    log_($"luautils::callGlobalFunction: {path}: Invalid return type");
                    return false;
                }
                return lua_.ToBoolean(-1);
            }
            catch (LuaException e)
            {
                log_($"luautils::callGlobalFunction: {path}: {e.Message}");
                return false;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        void ILatentHost.LatentWarning(string message) => log_(message);

        // ---- the callers -------------------------------------------------------------------------------------------

        // CBattleEntity::ForParty: each member of the char's party, or the char alone
        private static void LatentsForParty(CharData c, Action<CharData> f)
        {
            if (c.Party == null)
            {
                f(c);
                return;
            }
            foreach (var m in c.Party.Members.ToArray())
                f(m);
        }

        // CTimeServer::tick's per-char latent checks: every Vana'diel hour the hour's and the moon's, at midnight the
        // day of the week's, and when the time of day turns the day's and the job level's (the night multiples)
        private static void LatentsOnGameHour(CharData c, bool newDay, bool totdChanged)
        {
            c.Latents.CheckLatentsHours();
            c.Latents.CheckLatentsMoonPhase();
            if (newDay) c.Latents.CheckLatentsWeekDay();
            if (totdChanged)
            {
                c.Latents.CheckLatentsDay();
                c.Latents.CheckLatentsJobLevel();
            }
        }

        // ---- CLuaBaseEntity ----------------------------------------------------------------------------------------

        private void RegisterLatentBindings(Dictionary<string, LuaFunction> m)
        {
            // CLuaBaseEntity::addLatent(condID, conditionValue, mID, modValue)
            m["addLatent"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                c.Latents.AddLatentEffect((ushort)l.ToIntArg(2), (ushort)l.ToIntArg(3), (Mod)(ushort)l.ToIntArg(4), (short)l.ToIntArg(5));
                return 0;
            };
            // CLuaBaseEntity::delLatent: whether a matching latent was removed
            m["delLatent"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                return Ret(l, c.Latents.DelLatentEffect((ushort)l.ToIntArg(2), (ushort)l.ToIntArg(3), (Mod)(ushort)l.ToIntArg(4),
                    (short)l.ToIntArg(5)));
            };
            // CLuaBaseEntity::hasAllLatentsActive(slot)
            m["hasAllLatentsActive"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                return Ret(l, c.Latents.HasAllLatentsActive((byte)l.ToIntArg(2)));
            };
        }
    }
}
