-----------------------------------
-- Area: Lower Jeuno
--  NPC: Naruru
-- Starts and Finishes Quests: Cook's Pride
-- !pos -56 0.1 -138 245
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- Cook's Pride, The Lost Cardian, The Kind Cardian (one if-chain in the Lua)
    if
        xi.cs.quest(player, npc, 'jeuno.cooks_pride', 'onTrigger') or
        xi.cs.quest(player, npc, 'jeuno.the_lost_cardian', 'onTrigger') or
        xi.cs.quest(player, npc, 'jeuno.the_kind_cardian', 'onTrigger')
    then
        return
    else
        player:startEvent(98) -- Standard dialog
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 189 or csid == 188 or csid == 187 then
        xi.cs.questEvent(player, npc, 'jeuno.cooks_pride', csid, option)
    end
end

return entity
