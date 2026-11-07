-----------------------------------
-- Area: Northern San d'Oria
--  NPC: Miaux
-- Type: Quest Giver
-- !pos -169.127 2.999 158.677 231
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'sandoria.a_craftsmans_work', 'onTrigger') then
        player:startEvent(11)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 73 or csid == 71 or csid == 70 then
        xi.cs.questEvent(player, npc, 'sandoria.a_craftsmans_work', csid, option)
    end
end

return entity
