-----------------------------------
-- Area: Kazham
--  NPC: Rauteinot
-- Starts and Finishes Quest: Missionary Man
-- !pos -42 -10 -89 250
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'outlands.missionary_man', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'outlands.missionary_man', 'onTrigger') then
        return
    end

    player:startEvent(136) -- Standard dialog
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'outlands.missionary_man', csid, option)
end

return entity
