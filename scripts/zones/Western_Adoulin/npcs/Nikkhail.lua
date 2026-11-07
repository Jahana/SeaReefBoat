-----------------------------------
-- Area: Western Adoulin
--  NPC: Nikkhail
--  Involved With Quest: 'A Thirst for the Ages'
-- !pos -101 3 9 256
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    local soaMission = player:getCurrentMission(xi.mission.log_id.SOA)

    if soaMission >= xi.mission.id.soa.LIFE_ON_THE_FRONTIER then
        -- A Thirst for the Ages: 5053 while accepted, ATFTA_Status < 2, without the copy
        xi.cs.quest(player, npc, 'adoulin.a_thirst_for_the_ages', 'onTrigger')
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'adoulin.a_thirst_for_the_ages', csid, option)
end

return entity
