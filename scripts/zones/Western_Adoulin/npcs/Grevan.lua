-----------------------------------
-- Area: Western Adoulin
--  NPC: Grevan
-- Involved With Quest: 'Order Up'
-- !pos 50 0 6 256
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- Order Up: 69 while accepted without bit 10
    if xi.cs.quest(player, npc, 'adoulin.order_up', 'onTrigger') then
        return
    end

    if player:getQuestStatus(xi.questLog.ADOULIN, xi.quest.id.adoulin.HYPOCRITICAL_OATH) == xi.questStatus.QUEST_COMPLETED then
        if player:getQuestStatus(xi.questLog.ADOULIN, xi.quest.id.adoulin.NOTSOCLEAN_BILL) == xi.questStatus.QUEST_COMPLETED then
            -- Standard dialogue after stamping out plague in Svenja quest line
            player:startEvent(188)
        else
            -- Standard dialogue before stamping out plague in Svenja quest line
            player:startEvent(171)
        end
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'adoulin.order_up', csid, option)
end

return entity
