-----------------------------------
-- Area: Northern San d'Oria
--  NPC: Eperdur
-- Starts and Finishes Quest: Healing the Land,
-- !pos 129 -6 96 231
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'sandoria.healing_the_land', 'onTrigger') then
        return
    end

    xi.cs.quest(player, npc, 'sandoria.sorcery_of_the_north', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 681 or csid == 683 then
        xi.cs.questEvent(player, npc, 'sandoria.healing_the_land', csid, option)
    elseif csid == 685 or csid == 687 then
        xi.cs.questEvent(player, npc, 'sandoria.sorcery_of_the_north', csid, option)
    end
end

return entity
