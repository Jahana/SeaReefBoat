-- QuestScripts.Live p11: a target that is not there yet: an empty function is put in first, so super() can be called
require('modules/module_utils')
local m = Module:new('p11_newfn')
m:addOverride('xi.equipment.p11new', function()
    super()
    return 'new'
end)
