-----------------------------------
-- Area: Windurst Waters
--  NPC: Fuepepe
-- Involved in Quest: Class Reunion
-- !pos 161 -2 161 238
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- CLASS REUNION
    xi.cs.quest(player, npc, 'windurst.class_reunion', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 817 then
        xi.cs.questEvent(player, npc, 'windurst.class_reunion', csid, option)
    end
end

return entity
