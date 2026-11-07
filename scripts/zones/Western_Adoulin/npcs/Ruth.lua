-----------------------------------
-- Area: Western Adoulin
--  NPC: Ruth
-- Involved With Quest: 'A Pioneers Best (Imaginary) Friend'
-- !pos -144 4 -10 256
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if player:getCurrentMission(xi.mission.log_id.SOA) >= xi.mission.id.soa.LIFE_ON_THE_FRONTIER then
        -- A Pioneer's Best (Imaginary) Friend: 2523 while accepted without Ionis
        xi.cs.quest(player, npc, 'adoulin.a_pioneers_best_imaginary_friend', 'onTrigger')
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'adoulin.a_pioneers_best_imaginary_friend', csid, option)
end

return entity
