using System.Collections.Generic;
using XI.Persistence;
using XI.Script;
using LuaFunction = XI.Script.LuaFunction;

namespace XI.Zone
{
    // Bindings the zones' onInitialize and onZoneIn reach once the zone Lua runs by default (ZoneLuaEnabled): the
    // luautils global SetServerVariable (serverutils::SetServerVar / PersistServerVar), and the CLuaBaseEntity methods
    // findItem, getMoghouseFlag / setMoghouseFlag and getPreviousZoneLineID that xi.moghouse's zone-in reads
    // (and CLuaItem::isInstalled, in ZoneHost.Shop.cs). (CLuaZone::queryEntitiesByName, which xi.conquest's
    // toggleRegionalNPCs reads, is in ZoneHost.ZoneLua.cs.)
    // Not ported: PersistServerVar's read-back retries (map.SETVAR_RETRY_MAX). (Get / SetVolatileServerVariable are in
    // ZoneHost.LuautilsGlobals.cs.)
    public sealed partial class ZoneHost
    {
        private void RegisterZoneLuaGapGlobals()
        {
            // luautils::SetServerVariable(name, value, expiry = 0): an expiry already past is refused, else
            // serverutils::PersistServerVar (0 deletes the row)
            lua_.SetGlobal("SetServerVariable", l =>
            {
                string name = l.ToStringArg(1);
                int value = l.ToIntArg(2);
                uint expiry = l.IsNumber(3) ? l.ToUIntArg(3) : 0;
                if (expiry > 0 && expiry <= UnixNow())
                {
                    log_($"Attempting to set variable '{name}' with an expired time: {expiry}");
                    return 0;
                }
                if (value == 0) Exec("DELETE FROM server_variables WHERE name = ? LIMIT 1", BoundValue.Of(name));
                else
                    Exec("INSERT INTO server_variables VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE value = ?, expiry = ?",
                         BoundValue.Of(name), BoundValue.Of(value), BoundValue.Of(expiry), BoundValue.Of(value), BoundValue.Of(expiry));
                return 0;
            });
        }

        internal const string SqlPrevZoneLine = "SELECT pos_prevzonelineid FROM chars WHERE charid = ? LIMIT 1";

        private void RegisterZoneLuaGapBindings(Dictionary<string, LuaFunction> m)
        {
            // CLuaBaseEntity::findItem(itemID, location): the stack in that container (an invalid one reads the
            // inventory), else the first container holding the item; nil for a non-PC or no match
            m["findItem"] = l =>
            {
                ushort itemId = (ushort)l.ToUIntArg(2);
                Item found = null;
                if (Entity(l) is CharData c)
                {
                    if (!l.IsNil(3))
                    {
                        byte loc = (byte)l.ToUIntArg(3);
                        found = SearchItem(c, loc < CharData.MaxContainers ? (Container)loc : Container.Inventory, itemId);
                    }
                    else
                        for (int i = 0; i < CharData.MaxContainers && found == null; i++) found = SearchItem(c, (Container)i, itemId);
                }
                l.PushObject(found, ItemType);
                return 1;
            };

            // CLuaBaseEntity::getMoghouseFlag: profile.mhflag
            m["getMoghouseFlag"] = l => Ret(l, Pc(l, "getMoghouseFlag") is CharData c ? c.MhFlag : 0);

            // CLuaBaseEntity::setMoghouseFlag: profile.mhflag, then charutils::SaveCharStats
            m["setMoghouseFlag"] = l =>
            {
                ushort flag = (ushort)l.ToUIntArg(2);
                if (Pc(l, "setMoghouseFlag") is not CharData c) return 0;
                c.MhFlag = flag;
                SaveCharStats(c);
                return 0;
            };

            // CLuaBaseEntity::getPreviousZoneLineID: m_PrevZonelineID, which charutils::LoadChar reads from
            // chars.pos_prevzonelineid and only SavePrevZoneLineID (on a zone line, before the char leaves) writes; the
            // row is read here instead of a CharData field
            m["getPreviousZoneLineID"] = l =>
            {
                if (Pc(l, "getPreviousZoneLineID") is not CharData c) return Ret(l, 0);
                var r = Exec(SqlPrevZoneLine, BoundValue.Of(c.Id));
                return Ret(l, r != null && r.Next() ? r.Get<uint>("pos_prevzonelineid") : 0);
            };
        }
    }
}
