-- QuestScripts.Live p11: a folder's sub folder (the expansion is recursive); its target's table is missing
require('modules/module_utils')
P11Order = P11Order or {}
table.insert(P11Order, 'c')
local m = Module:new('p11_order_c')
m:addOverride('xi.p11missing.fn', function()
end)
