-- QuestScripts.Live p11: modifyInteractionEntry of a C# quest's container (The Elvaan Goldsmith, its Lua file gone) and
-- of a Lua quest's: the shared fixture's (P11FixtureLuaFile, set by PlatformP11.cs from FixtureLuaFile before the call)
require('modules/module_utils')
local m = Module:new('p11_patch')
m:addOverride('xi.server.onServerStart', function()
    super()
    _G.P11Patch = {}
    xi.module.modifyInteractionEntry('scripts/quests/bastok/The_Elvaan_Goldsmith', function(quest)
        _G.P11Patch.elvaan = true
    end)
    local fixture = rawget(_G, 'P11FixtureLuaFile')
    if fixture then
        xi.module.modifyInteractionEntry(fixture, function(quest)
            _G.P11Patch.fixture = type(quest.sections) == 'table'
        end)
    end
end)
