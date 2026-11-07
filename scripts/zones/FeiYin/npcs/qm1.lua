-----------------------------------
-- Area: FeiYin
--  NPC: qm1 (???)
-- Involved In Quest: Pieuje's Decision
-- !pos -55 -16 69 204
-----------------------------------
local ID = zones[xi.zone.FEIYIN]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.pieujes_decision', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'sandoria.pieujes_decision', 'onTrigger') then
        return
    end

    player:messageSpecial(ID.text.YOU_FIND_NOTHING)
end

return entity
