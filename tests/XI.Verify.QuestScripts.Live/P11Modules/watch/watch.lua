-- QuestScripts.Live p11w: the file watcher's probes (PlatformP11Watch.cs writes them): a global
-- (scripts/globals/p11w_probe.lua, xi.p11w_global) and an item script (scripts/items/p11w_probe.lua)
require('modules/module_utils')
local m = Module:new('p11w_watch')
m:addOverride('xi.p11w_global.fn', function()
    return 'mod(' .. super() .. ')'
end)
m:addOverride('xi.items.p11w_probe.onProbe', function()
    return 'mod(' .. super() .. ')'
end)
