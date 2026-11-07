-----------------------------------
-- Area: Kazham
--  NPC: Kukupp
-----------------------------------
---@type TNpcEntity
local entity = {}

local pathNodes =
{
    { x = 44.089, y = -11.000, z = -178.976, wait = 5000 },
    { x = 40.132, y = -11.000, z = -179.322, wait = 5000 },
    { x = 44.089, y = -11.000, z = -178.976, wait = 5000 },
    { x = 40.132, y = -11.000, z = -176.322, wait = 5000 },
    { x = 46.417, y = -11.000, z = -176.672, wait = 5000 },
    { x = 40.132, y = -11.000, z = -176.322, wait = 5000 },
    { x = 46.417, y = -11.000, z = -176.672, wait = 5000 },
    { x = 40.132, y = -11.000, z = -176.322, wait = 5000 },
    { x = 46.417, y = -11.000, z = -176.672, wait = 5000 },
    { x = 40.132, y = -11.000, z = -176.322, wait = 5000 },
}

entity.onSpawn = function(npc)
    npc:initNpcAi()
    npc:setPos(xi.path.first(pathNodes))
    npc:pathThrough(pathNodes, bit.bor(xi.path.flag.PATROL, xi.path.flag.RUN))
end

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'outlands.the_opo_opo_and_i', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'outlands.the_opo_opo_and_i', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 220 or csid == 230 then
        xi.cs.questEvent(player, npc, 'outlands.the_opo_opo_and_i', csid, option)
    end
end

return entity
