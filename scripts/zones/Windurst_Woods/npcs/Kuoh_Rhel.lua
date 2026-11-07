-----------------------------------
-- Area: Windurst Woods
--  NPC: Kuoh Rhel
-- Starts quests: Chocobilious, In a Stew
-- !pos 131.437 -6 -102.723 241
--  Note: In a Stew should only repeat once per conquest tally. The tally is not implemented at time of
--        writing this quest. Once it is working please feel free to add it in ^^
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- CHOCOBILIOUS
    xi.cs.quest(player, npc, 'windurst.chocobilious', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    -- CHOCOBILIOUS
    xi.cs.questEvent(player, npc, 'windurst.chocobilious', csid, option)
end

return entity
