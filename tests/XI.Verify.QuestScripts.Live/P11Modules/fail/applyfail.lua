-- QuestScripts.Live p11: an override module_utils' applyOverride raises on (its func not a function: setfenv fails),
-- so it is logged and left not applied
require('modules/module_utils')
xi.p11applyfail = xi.p11applyfail or {}
local m = Module:new('p11_applyfail')
m:addOverride('xi.p11applyfail.fn', function()
end)
m.overrides[1].func = {}
