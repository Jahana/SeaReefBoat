-- QuestScripts.Live p11: listed as a file and inside a listed folder, so it runs twice; wraps what is there with + 1
require('modules/module_utils')
P11Order = P11Order or {}
table.insert(P11Order, 'a')
local m = Module:new('p11_order_a')
m:addOverride('xi.equipment.vigilWeaponRequiredWsPoints', function(player)
    return super(player) + 1
end)
