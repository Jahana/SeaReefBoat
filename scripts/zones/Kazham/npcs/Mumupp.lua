-----------------------------------
-- Area: Kazham
--  NPC: Mumupp
-----------------------------------
---@type TNpcEntity
local entity = {}

local pathNodes =
{
    { x = 94.732452, y = -15.000000, z = -114.034622 },
    { x = 94.210846, z = -114.989388 },
    { x = 93.508865, z = -116.274101 },
    { x = 94.584877, z = -116.522118 },
    { x = 95.646988, z = -116.468452 },
    { x = 94.613518, z = -116.616562 },
    { x = 93.791100, z = -115.858505 },
    { x = 94.841835, z = -116.108437 },
    { x = 93.823380, z = -116.712860 },
    { x = 94.986847, z = -116.571831 },
    { x = 94.165512, z = -115.965698 },
    { x = 95.005806, z = -116.519707 },
    { x = 93.935555, z = -116.706291 },
    { x = 94.943497, z = -116.578346 },
    { x = 93.996826, z = -115.932816 },
    { x = 95.060165, z = -116.180840 },
    { x = 94.081062, z = -115.923836 },
    { x = 95.246490, z = -116.215691 },
    { x = 94.234077, z = -115.960793 },
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
    if csid == 221 or csid == 231 then
        xi.cs.questEvent(player, npc, 'outlands.the_opo_opo_and_i', csid, option)
    end
end

return entity
