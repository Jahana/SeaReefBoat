-----------------------------------
-- Area: Kazham
--  NPC: Nenepp
-----------------------------------
---@type TNpcEntity
local entity = {}

local pathNodes =
{
    { x = 29.014000, y = -11.00000, z = -183.884000 },
    { x = 31.023000, z = -183.538000 },
    { x = 33.091000, z = -183.738000 },
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
    if csid == 241 or csid == 238 then
        xi.cs.questEvent(player, npc, 'outlands.the_opo_opo_and_i', csid, option)
    end
end

return entity
