-----------------------------------
-- Area: Lower Jeuno
--  NPC: Teigero-Bangero
-- Involved in Quests: The Lost Cardian
-- !pos -58 0 -143 245
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- The Wonder Magic Set, Cook's Pride, The Lost Cardian, The Kind Cardian (one if-chain in the Lua, with no else)
    if
        xi.cs.quest(player, npc, 'jeuno.the_wonder_magic_set', 'onTrigger') or
        xi.cs.quest(player, npc, 'jeuno.cooks_pride', 'onTrigger') or
        xi.cs.quest(player, npc, 'jeuno.the_lost_cardian', 'onTrigger') or
        xi.cs.quest(player, npc, 'jeuno.the_kind_cardian', 'onTrigger')
    then
        return
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 29 then
        xi.cs.questEvent(player, npc, 'jeuno.the_lost_cardian', csid, option)
    end
end

return entity
