-----------------------------------
-- Area: Port Windurst
--  NPC: Ohruru
-- Starts & Finishes Repeatable Quest: Catch me if you can
-- Involved in Quest: Wonder Wands
-- Note: Animation for his "Cure" is not functioning. Unable to capture option 1, so if the user says no, he heals them anyways.
-- !pos -108 -5 94 240
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- Catch It If You Can (and its Wonder Wands lines)
    if not xi.cs.quest(player, npc, 'windurst.catch_it_if_you_can', 'onTrigger') then
        player:startEvent(230) -- STANDARD CONVERSATION
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.catch_it_if_you_can', csid, option)
end

return entity
