-----------------------------------
-- Area: Northern San d'Oria
--  NPC: Excenmille
-- Type: Trust NPC, Ballista Pursuivant
-- !pos -229.344 6.999 22.976 231
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'sandoria.trust_sandoria', 'onTrigger') then
        return
    else
        player:startEvent(29)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    -- TRUST
    if csid == 893 or csid == 895 or csid == 897 then
        xi.cs.questEvent(player, npc, 'sandoria.trust_sandoria', csid, option)
    end
end

return entity
