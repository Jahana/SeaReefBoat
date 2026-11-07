-----------------------------------
-- Area: Lower Jeuno
--  NPC: Panta-Putta
-- Starts and Finishes Quest: The Wonder Magic Set, The kind cardian
-- Involved in Quests: The Lost Cardian
-- !pos -61 0 -140 245
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- The Wonder Magic Set, The Lost Cardian, The Kind Cardian (one if-chain in the Lua)
    if
        xi.cs.quest(player, npc, 'jeuno.the_wonder_magic_set', 'onTrigger') or
        xi.cs.quest(player, npc, 'jeuno.the_lost_cardian', 'onTrigger') or
        xi.cs.quest(player, npc, 'jeuno.the_kind_cardian', 'onTrigger')
    then
        return
    else
        player:startEvent(78) -- Base standard dialog
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 77 or csid == 33 then
        xi.cs.questEvent(player, npc, 'jeuno.the_wonder_magic_set', csid, option)
    elseif csid == 30 then
        xi.cs.questEvent(player, npc, 'jeuno.the_lost_cardian', csid, option)
    elseif csid == 35 then
        xi.cs.questEvent(player, npc, 'jeuno.the_kind_cardian', csid, option)
    end
end

return entity
