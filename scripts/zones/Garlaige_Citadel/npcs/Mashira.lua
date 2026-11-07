-----------------------------------
-- Area: Garlaige Citadel
--  NPC: Mashira
-- Involved in Quests: Rubbish day, Making Amens!
-- !pos 141 -6 138 200
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.making_amens', 'onTrigger') then
        player:startEvent(11, 3) -- Standard dialog and menu
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.making_amens', csid, option)
end

return entity
