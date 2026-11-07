-----------------------------------
-- Area: Ru'Lude Gardens
--  NPC: Laityn
-- Involved In Quest: Recollections
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.recollections', 'onTrigger') then
        player:startEvent(10006)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.recollections', csid, option)
end

return entity
