-----------------------------------
-- Area: Kazham
--  NPC: Hari Pakhroib
-- Starts and Finishes Quest: Greetings to the Guardian
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'outlands.greetings_to_the_guardian', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'outlands.greetings_to_the_guardian', csid, option)
end

return entity
