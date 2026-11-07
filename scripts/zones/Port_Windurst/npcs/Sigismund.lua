-----------------------------------
-- Area: Port Windurst
--  NPC: Sigismund
-- Starts and Finishes Quest: To Catch a Falling Star
-- !pos -110 -10 82 240
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.to_catch_a_falling_star', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.to_catch_a_falling_star', 'onTrigger') then
        player:startEvent(357)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.to_catch_a_falling_star', csid, option)
end

return entity
