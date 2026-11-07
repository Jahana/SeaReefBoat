-- QuestScripts.Live p11: a Module whose enabled is not a boolean (registerModule skips it with an error)
require('modules/module_utils')
local m = Module:new('p11_notbool')
m:addOverride('xi.p11notbool.fn', function()
end)
m.enabled = 'yes'
