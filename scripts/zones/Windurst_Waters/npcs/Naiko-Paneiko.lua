-----------------------------------
-- Area: Windurst Waters
--  NPC: Naiko-Paneiko
-- Involved In Quest: Making Headlines, Riding on the Clouds
-- !pos -246 -5 -308 238
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.making_headlines', 'onTrigger') then
        player:startEvent(663) -- Standard conversation
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.making_headlines', csid, option)
end

return entity
