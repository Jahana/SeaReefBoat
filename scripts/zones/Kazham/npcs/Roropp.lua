-----------------------------------
-- Area: Kazham
--  NPC: Roropp
-----------------------------------
---@type TNpcEntity
local entity = {}

local pathNodes =
{
    { x = 17.314, y = -8.000, z = -110.064, wait = 5000 },
    { x = 13.323, z = -102.821, wait = 5000 },
    { x = 17.314, z = -110.064, wait = 5000 },
    { x = 12.576, z = -113.211, wait = 5000 },
    { x = 17.314, z = -110.064, wait = 5000 },
    { x = 13.323, z = -102.821, wait = 5000 },
    { x = 17.388, z = -99.219, wait = 5000 },
    { x = 15.511, z = -112.292, wait = 5000 },
    { x = 13.605, z = -109.863, wait = 5000 },
    { x = 15.511, z = -112.292, wait = 5000 },
    { x = 13.605, z = -109.863, wait = 5000 },
    { x = 17.388, z = -99.219, wait = 5000 },
    { x = 13.323, z = -102.821, wait = 5000 },
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
    if csid == 222 or csid == 232 then
        xi.cs.questEvent(player, npc, 'outlands.the_opo_opo_and_i', csid, option)
    end
end

return entity
