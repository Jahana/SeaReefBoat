-- QuestScripts.Live p11: loaded from the folder line only; doubles what is there
require('modules/module_utils')
P11Order = P11Order or {}
table.insert(P11Order, 'b')
local m = Module:new('p11_order_b')
m:addOverride('xi.equipment.vigilWeaponRequiredWsPoints', function(player)
    return super(player) * 2
end)
