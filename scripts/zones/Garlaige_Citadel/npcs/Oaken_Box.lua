-----------------------------------
-- Area: Garlaige Citadel
--  NPC: Oaken Box
-- Involved In Quest: Peace for the Spirit
-- !pos -164 0.1 225 200
-----------------------------------
local ID = zones[xi.zone.GARLAIGE_CITADEL]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.peace_for_the_spirit', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if GetMobByID(ID.mob.GUARDIAN_STATUE):isAlive() then
        player:messageSpecial(ID.text.FEELS_WRONG)
    elseif xi.cs.quest(player, npc, 'sandoria.peace_for_the_spirit', 'onTrigger') then
        return
    else
        player:messageSpecial(ID.text.BOXES_HERE)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'sandoria.peace_for_the_spirit', csid, option)
end

return entity
