-----------------------------------
-- Area: Windurst Woods
--  NPC: Wani Casdohry
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    local mihgosAmigo = player:getQuestStatus(xi.questLog.WINDURST, xi.quest.id.windurst.MIHGOS_AMIGO)

    if xi.cs.quest(player, npc, 'windurst.twinstone_bonding', 'onTrigger') then
        return
    elseif mihgosAmigo == xi.questStatus.QUEST_ACCEPTED then
        player:startEvent(86, 0, 498)
    else
        player:startEvent(425)
    end
end

return entity
