-- QuestScripts.Live p11: targets whose tables are missing: one named by a global file's bare stem (TryApplyLuaModules'
-- bareKey), one under a global file's xi path that the file does not define (scripts/globals/canusespell_override.lua)
require('modules/module_utils')
local m = Module:new('p11_bare')
m:addOverride('equipment.p11', function()
end)
m:addOverride('xi.canusespell_override.p11', function()
end)
