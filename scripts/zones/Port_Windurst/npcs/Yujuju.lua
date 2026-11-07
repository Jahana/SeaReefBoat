-----------------------------------
-- Area: Port Windurst
--  NPC: Yujuju
--  Involved In Quest: Making Headlines
-- !pos 201.523 -4.785 138.978 240
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.making_headlines', 'onTrigger') then
        player:startEvent(340) -- Standard Conversation
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.making_headlines', csid, option)
end

return entity
