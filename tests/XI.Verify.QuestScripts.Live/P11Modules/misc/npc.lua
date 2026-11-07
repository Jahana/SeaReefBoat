-- QuestScripts.Live p11: an NPC script's function (Reet, Bastok Markets: loaded with the zone)
require('modules/module_utils')
local m = Module:new('p11_npc')
m:addOverride('xi.zones.Bastok_Markets.npcs.Reet.onTrigger', function(player, npc)
    -- an override runs in an environment of its own (its super): a global is set through _G
    _G.P11Npc = (_G.P11Npc or '') .. 'module;'
    return super(player, npc)
end)
