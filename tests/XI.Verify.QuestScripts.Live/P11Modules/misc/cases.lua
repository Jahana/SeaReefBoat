-- QuestScripts.Live p11: a case whose era content is on (RESTRICT_CONTENT 0): declared, not applied
require('modules/module_utils')
local m = Module:new('p11_cases')
m:addOverrideByEra('xi.equipment.vigilWeaponRequiredWsPoints', {
    [xi.expansion.ROV] = function(player)
        return 0
    end,
})
