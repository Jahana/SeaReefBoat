-----------------------------------
-- Area: Fort_Karugo-Narugo_[S]
--  NPC: Pecca-Pocca
-- Involved In Quest: REQUIEM_FOR_THE_DEPARTED
-- !pos -163 -68 -155 96
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'crystalWar.requiem_for_the_departed', 'onTrigger') then
        return
    end

    player:startEvent(233) -- standard dialogue (also after receiving KI)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'crystalWar.requiem_for_the_departed', csid, option)
end

return entity
