-----------------------------------
-- Area: Windurst Woods
--  NPC: Perih Vashai
-- Involved in Quests: Sin Hunting (RNG AF1), Fire and Brimstone (RNG AF2), Unbridled Passion (RNG AF3)
-- !pos 117 -3 92 241
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    -- FIRE AND BRIMSTONE
    xi.cs.quest(player, npc, 'windurst.fire_and_brimstone', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    -- FIRE AND BRIMSTONE, then UNBRIDLED PASSION (the Lua's arm order)
    if xi.cs.quest(player, npc, 'windurst.fire_and_brimstone', 'onTrigger') then
        return
    end

    xi.cs.quest(player, npc, 'windurst.unbridled_passion', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    -- FIRE AND BRIMSTONE
    if csid == 531 or csid == 535 or csid == 537 then
        xi.cs.questEvent(player, npc, 'windurst.fire_and_brimstone', csid, option)

    -- UNBRIDLED PASSION
    elseif csid == 541 or csid == 546 then
        xi.cs.questEvent(player, npc, 'windurst.unbridled_passion', csid, option)
    end
end

return entity
