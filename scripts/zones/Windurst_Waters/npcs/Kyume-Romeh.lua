-----------------------------------
-- Area: Windurst Waters
--  NPC: Kyume-Romeh
--  Involved In Quest: Making Headlines, Hat in Hand
-- !pos -58 -4 23 238
-----------------------------------
local ID = zones[xi.zone.WINDURST_WATERS]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if
        player:hasKeyItem(xi.keyItem.NEW_MODEL_HAT) and
        not utils.mask.getBit(player:getCharVar('QuestHatInHand_var'), 4)
    then
        player:messageSpecial(ID.text.YOU_SHOW_OFF_THE, 0, xi.keyItem.NEW_MODEL_HAT)
        player:startEvent(60)
    elseif xi.cs.quest(player, npc, 'windurst.making_headlines', 'onTrigger') then
        return
    else
        if math.randomInt(1, 100) <= 50 then
            player:startEvent(604) -- Standard Conversation
        else
            player:startEvent(393) -- Standard Conversation
        end
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 668 then
        xi.cs.questEvent(player, npc, 'windurst.making_headlines', csid, option)
    elseif csid == 60 then
        player:setCharVar('QuestHatInHand_var', utils.mask.setBit(player:getCharVar('QuestHatInHand_var'), 4, true))
        player:incrementCharVar('QuestHatInHand_count', 1)
    end
end

return entity
