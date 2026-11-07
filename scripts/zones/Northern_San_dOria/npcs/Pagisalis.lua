-----------------------------------
-- Area: Northern San d'Oria
--  NPC: Pagisalis
-- Involved In Quest: Enveloped in Darkness
-- !zone 231
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.undying_flames', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'sandoria.undying_flames', 'onTrigger') then
        return
    end

    player:startEvent(564)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'sandoria.undying_flames', csid, option)
end

return entity
