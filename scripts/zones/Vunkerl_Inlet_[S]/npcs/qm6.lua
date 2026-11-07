-----------------------------------
-- Area: Vunkerl_Inlet_[S]
--  NPC: qm6 (???)
-- Involved In Quest: EVIL_AT_THE_INLET
-- !pos -636 -51 -454 83
-----------------------------------
local ID = zones[xi.zone.VUNKERL_INLET_S]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'crystalWar.evil_at_the_inlet', 'onTrigger') then
        return
    end

    player:messageSpecial(ID.text.NOTHING_OUT_OF_ORDINARY)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'crystalWar.evil_at_the_inlet', csid, option)
end

return entity
