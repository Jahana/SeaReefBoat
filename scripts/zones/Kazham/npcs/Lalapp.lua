-----------------------------------
-- Area: Kazham
--  NPC: Lalapp
-----------------------------------
---@type TNpcEntity
local entity = {}

local pathNodes =
{
    { x = -63.243702, y = -11.000, z = -97.916130 },
    { x = -63.970551, z = -97.229286 },
    { x = -64.771614, z = -96.499062 },
}

entity.onSpawn = function(npc)
    npc:initNpcAi()
    npc:setPos(xi.path.first(pathNodes))
    npc:pathThrough(pathNodes, xi.path.flag.PATROL)
end

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'outlands.the_opo_opo_and_i', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'outlands.the_opo_opo_and_i', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 227 or csid == 237 then
        xi.cs.questEvent(player, npc, 'outlands.the_opo_opo_and_i', csid, option)
    end
end

return entity
