-----------------------------------
-- Area: Port San d'Oria
--  NPC: Arminibit
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'sandoria.the_holy_crest', 'onTrigger') then
        player:startEvent(587)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 24 then
        xi.cs.questEvent(player, npc, 'sandoria.the_holy_crest', csid, option)
    end
end

return entity
