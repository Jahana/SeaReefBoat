-----------------------------------
-- Area: Windurst Waters
--  NPC: Ranpi-Monpi
-- Starts and Finishes Quest: A Crisis in the Making
-- Involved in quest: In a Stew, For Want of a Pot, The Dawn of Delectability
-- !pos -116 -3 52  238
-- (outside the shop he is in)
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- A Crisis in the Making
    if xi.cs.quest(player, npc, 'windurst.a_crisis_in_the_making', 'onTrigger') then
        return
    else
    --Standard dialogs
        local rand = math.randomInt(1, 3)
        if rand == 1 then  -- STANDARD CONVO: sings song about ingredients
            player:startEvent(249)
        elseif rand == 2 then   -- STANDARD CONVO 2: sings song about ingredients
            player:startEvent(251)
        elseif rand == 3 then   -- STANDARD CONVO 3:
            player:startEvent(256)
        end
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    -- A Crisis in the Making
    if csid == 258 or csid == 259 or csid == 267 or csid == 268 then
        xi.cs.questEvent(player, npc, 'windurst.a_crisis_in_the_making', csid, option)
    end
end

return entity
