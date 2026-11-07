-----------------------------------
-- Area: Windurst Walls
--  NPC: Koru-Moru
-- Starts & Ends Quest: Star Struck
-- Involved in Quest: Making the Grade, Riding on the Clouds
-- !pos -120 -6 124 239
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    local count = trade:getItemCount()

    if
        trade:hasItemQty(xi.item.SQUARE_OF_SILK_CLOTH, 1) and
        count == 1 and
        trade:getGil() == 0
    then
        xi.cs.quest(player, npc, 'windurst.the_root_of_the_problem', 'onTrade', trade)
    elseif
        trade:hasItemQty(xi.item.ASTRAGALOS, 4) and
        count == 4 and
        trade:getGil() == 0
    then
        xi.cs.quest(player, npc, 'windurst.class_reunion', 'onTrade', trade)
    end
end

entity.onTrigger = function(player, npc)
    -----------------------------------
    -- Carbuncle Debacle
    if xi.cs.quest(player, npc, 'windurst.carbuncle_debacle', 'onTrigger') then
        return
    -----------------------------------
    -- Class Reunion
    elseif xi.cs.quest(player, npc, 'windurst.class_reunion', 'onTrigger') then
        return
    -----------------------------------
    else
        xi.cs.quest(player, npc, 'windurst.the_root_of_the_problem', 'onTrigger')
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 412 or csid == 407 or csid == 410 then
        xi.cs.questEvent(player, npc, 'windurst.class_reunion', csid, option)
    elseif csid == 416 or csid == 417 or csid == 419 then
        xi.cs.questEvent(player, npc, 'windurst.carbuncle_debacle', csid, option)
    end
end

return entity
