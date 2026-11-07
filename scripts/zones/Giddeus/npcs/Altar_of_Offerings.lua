-----------------------------------
-- Area: Giddeus
--  NPC: Alter of Offerings
-- Involved in Quest: A Crisis in the Making
-- !pos -137 17 177 145
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- A Crisis in the Making
    if xi.cs.quest(player, npc, 'windurst.a_crisis_in_the_making', 'onTrigger') then
        return
    else
        player:startEvent(60) -- Standard Message
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    -- A Crisis in the Making
    if csid == 53 then
        xi.cs.questEvent(player, npc, 'windurst.a_crisis_in_the_making', csid, option)
    end
end

return entity
