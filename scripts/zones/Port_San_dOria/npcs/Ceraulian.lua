-----------------------------------
-- Area: Port San d'Oria
--  NPC: Ceraulian
-- Involved in Quests: The Holy Crest, Knight Stalker
-- !pos 0 -8 -122 232
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'sandoria.the_holy_crest', 'onTrigger') then
        return

    -- Knight Stalker (DRG AF3)
    elseif xi.cs.quest(player, npc, 'sandoria.knight_stalker', 'onTrigger') then
        return

    else
        player:startEvent(587)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 24 then
        xi.cs.questEvent(player, npc, 'sandoria.the_holy_crest', csid, option)

    -- Knight Stalker (DRG AF3)
    elseif csid == 19 or csid == 22 then
        xi.cs.questEvent(player, npc, 'sandoria.knight_stalker', csid, option)
    end
end

return entity
