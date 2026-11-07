-----------------------------------
-- Area: Southern SandOria [S]
--  NPC: Thierride
-- !pos -124 -2 14 80
-----------------------------------
---@type TNpcEntity
local entity = {}

-- Item 1019 = Lufet Salt
-- Had to use setCharVar because you have to trade Salts one at a time according to the wiki.
-- Lufet Salt can be obtained by killing Crabs in normal West Ronfaure.

entity.onTrade = function(player, npc, trade)
    if xi.cs.quest(player, npc, 'crystalWar.beans_ahoy', 'onTrade', trade) then
        return
    end

    player:startEvent(339) -- Wrong Item Traded
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'crystalWar.beans_ahoy', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'crystalWar.beans_ahoy', csid, option)
end

return entity
