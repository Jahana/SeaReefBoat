-----------------------------------
-- Area: Windurst Walls (239)
--  NPC: Shantotto
-- !pos 122 -2 112 239
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'windurst.class_reunion', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 409 then
        xi.cs.questEvent(player, npc, 'windurst.class_reunion', csid, option)
    end
end

return entity
