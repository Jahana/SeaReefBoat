-----------------------------------
-- Area: Rabao
--  NPC: Edigey
-- Starts and Ends Quest: Don't Forget the Antidote
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'outlands.dont_forget_the_antidote', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'outlands.dont_forget_the_antidote', 'onTrigger') then
        return
    end

    player:startEvent(50)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'outlands.dont_forget_the_antidote', csid, option)
end

return entity
