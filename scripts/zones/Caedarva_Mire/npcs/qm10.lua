-----------------------------------
-- Area: Caedarva Mire
--  NPC: qm10
-- Involved in quest: Operation Teatime
-- !pos 473 -31 75 79
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'ahtUrhgan.operation_teatime', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 15 then
        xi.cs.questEvent(player, npc, 'ahtUrhgan.operation_teatime', csid, option)
    end
end

return entity
