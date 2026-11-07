-----------------------------------
-- Area: Windurst Walls
--  NPC: Hiwon-Biwon
-- Involved In Quest: Making Headlines, Curses, Foiled...Again!?
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- Making Headlines
    if xi.cs.quest(player, npc, 'windurst.making_headlines', 'onTrigger') then
        return

    -- default dialog
    else
        local rand = math.randomInt(1, 5)

        if rand == 1 then
            player:startEvent(305)
        elseif rand == 2 then
            player:startEvent(306)
        elseif rand == 3 then
            player:startEvent(168)
        elseif rand == 4 then
            player:startEvent(170)
        elseif rand == 5 then
            player:startEvent(169)
        end
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    -- Making Headlines
    xi.cs.questEvent(player, npc, 'windurst.making_headlines', csid, option)
end

return entity
