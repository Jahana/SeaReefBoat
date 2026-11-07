-----------------------------------
-- Area: Vunkerl_Inlet_[S]
--  NPC: qm4 (???)
-- Involved In Quest: REDEEMING_ROCKS
-- !pos -412 -29 -45 83
-----------------------------------
local ID = zones[xi.zone.VUNKERL_INLET_S]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'crystalWar.redeeming_rocks', 'onTrigger') then
        return
    end

    player:messageSpecial(ID.text.NOTHING_OUT_OF_ORDINARY)
end

return entity
