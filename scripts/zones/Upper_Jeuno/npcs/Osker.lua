-----------------------------------
-- Area: Upper Jeuno
--  NPC: Osker
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    local aNewDawnEvent = player:getCharVar('ANewDawn_Event')

    if
        trade:hasItemQty(xi.item.PIECE_OF_MAHOGANY_LUMBER, 1) and
        trade:getItemCount() == 1 and
        aNewDawnEvent == 3
    then
        player:tradeComplete()
        player:startEvent(148)
    end
end

entity.onTrigger = function(player, npc)
    -- A New Dawn
    xi.cs.quest(player, npc, 'jeuno.a_new_dawn', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    local aNewDawnEvent = player:getCharVar('ANewDawn_Event')

    if csid == 146 then
        if aNewDawnEvent == 2 then
            player:setCharVar('ANewDawn_Event', 3)
        end
    elseif csid == 148 then
        npcUtil.giveKeyItem(player, xi.keyItem.TAMERS_WHISTLE)
        player:setCharVar('ANewDawn_Event', 4)
    end
end

return entity
