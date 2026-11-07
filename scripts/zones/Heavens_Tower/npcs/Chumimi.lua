-----------------------------------
-- Area: Heaven's Tower
--  NPC: Chumimi
-- Starts and Finishes Quest: The Three Magi, Recollections
-- !pos 0.1 30 21 242
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    -- THE THREE MAGI, then RECOLLECTIONS, then THE ROOT OF THE PROBLEM (the Lua's arm order)
    if xi.cs.quest(player, npc, 'windurst.the_three_magi', 'onTrade', trade) then
        return
    end

    if xi.cs.quest(player, npc, 'windurst.recollections', 'onTrade', trade) then
        return
    end

    xi.cs.quest(player, npc, 'windurst.the_root_of_the_problem', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    -- THE THREE MAGI, then RECOLLECTIONS, then THE ROOT OF THE PROBLEM (the Lua's arm order)
    if xi.cs.quest(player, npc, 'windurst.the_three_magi', 'onTrigger') then
        return
    end

    if xi.cs.quest(player, npc, 'windurst.recollections', 'onTrigger') then
        return
    end

    if xi.cs.quest(player, npc, 'windurst.the_root_of_the_problem', 'onTrigger') then
        return
    end

    player:startEvent(259) -- Standard dialog
end

entity.onEventFinish = function(player, csid, option, npc)
    -- THE THREE MAGI
    if csid == 260 or csid == 269 then
        xi.cs.questEvent(player, npc, 'windurst.the_three_magi', csid, option)

    -- RECOLLECTIONS
    elseif csid == 270 or csid == 271 or csid == 275 then
        xi.cs.questEvent(player, npc, 'windurst.recollections', csid, option)

    -- THE ROOT OF THE PROBLEM (278 has no finish)
    elseif csid == 276 or csid == 279 or csid == 281 then
        xi.cs.questEvent(player, npc, 'windurst.the_root_of_the_problem', csid, option)
    end
end

return entity
