-----------------------------------
-- Area: Norg
--  NPC: Stray Cloud
-- Starts and Ends Quest: An Undying Pledge
-- !pos-20.617, 1.097, -29.165, 133
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'outlands.an_undying_pledge', 'onTrigger') then
        return
    end

    player:startEvent(231) -- Standard Conversation
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'outlands.an_undying_pledge', csid, option)
end

return entity
