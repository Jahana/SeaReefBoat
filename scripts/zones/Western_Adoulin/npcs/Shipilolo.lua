-----------------------------------
-- Area: Western Adoulin
--  NPC: Shipilolo
--  Involved with Quests: 'A Certain Substitute Patrolman'
--                        'Fertile Ground'
--                        'The Old Man and the Harpoon'
--                        'Wayward Waypoints'
-- !pos 84 0 -60 256
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if player:getCurrentMission(xi.mission.log_id.SOA) >= xi.mission.id.soa.LIFE_ON_THE_FRONTIER then
        -- The Old Man and the Harpoon (2543), then Fertile Ground (2850), then Wayward Waypoints (79)
        if
            not xi.cs.quest(player, npc, 'adoulin.the_old_man_and_the_harpoon', 'onTrigger') and
            not xi.cs.quest(player, npc, 'adoulin.fertile_ground', 'onTrigger')
        then
            xi.cs.quest(player, npc, 'adoulin.wayward_waypoints', 'onTrigger')
        end
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 2543 then
        xi.cs.questEvent(player, npc, 'adoulin.the_old_man_and_the_harpoon', csid, option)
    elseif csid == 2850 then
        xi.cs.questEvent(player, npc, 'adoulin.fertile_ground', csid, option)
    elseif csid == 79 then
        xi.cs.questEvent(player, npc, 'adoulin.wayward_waypoints', csid, option)
    end
end

return entity
