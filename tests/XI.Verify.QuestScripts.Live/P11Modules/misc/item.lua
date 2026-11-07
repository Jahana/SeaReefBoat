-- QuestScripts.Live p11: an item script's function (Treat Staff, loaded on its first use)
require('modules/module_utils')
local m = Module:new('p11_item')
m:addOverride('xi.items.treat_staff.onItemEquip', function(user, item)
    _G.P11Item = (_G.P11Item or '') .. 'module;'
    super(user, item)
end)
