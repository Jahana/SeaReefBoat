-- QuestScripts.Live p11: the file after one that failed: only its own Module is registered
require('modules/module_utils')
local m = Module:new('p11_after_boom')
m:addOverride('xi.p11afterboom.fn', function()
end)
