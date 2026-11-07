-- QuestScripts.Live p11: registers a Module, then fails to load
require('modules/module_utils')
local m = Module:new('p11_boom')
m:addOverride('xi.equipment.vigilWeaponRequiredWsPoints', function(player)
    return 0
end)
error('p11 boom')
