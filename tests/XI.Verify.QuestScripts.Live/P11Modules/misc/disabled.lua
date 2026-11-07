-- QuestScripts.Live p11: a module whose condition is false: declared, not applied
require('modules/module_utils')
local m = Module:new('p11_disabled', false)
m:addOverride('xi.equipment.vigilWeaponRequiredWsPoints', function(player)
    return 0
end)
