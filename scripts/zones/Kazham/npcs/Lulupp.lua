-----------------------------------
-- Area: Kazham
--  NPC: Lulupp
-- !pos -26.567 -3.5 -3.544 250
-----------------------------------
---@type TNpcEntity
local entity = {}

local pathNodes =
{
    { x = -27.457125, y = -3.043032, z = -22.057966 },
    { x = -27.373426, y = -2.772481, z = -20.974442 },
    { x = -27.103289, y = -2.500000, z = -17.846378 },
    { x = -26.864126, z = -15.667570 },
    { x = -26.532335, z = -16.636086 },
    { x = -26.505196, z = -15.471632 },
    { x = -26.509424, z = -14.359641 },
    { x = -26.564587, z = -4.499783 },
    { x = -26.574417, z = -5.523735 },
    { x = -26.580530, z = -6.591716 },
    { x = -26.583765, z = -8.555706 },
    { x = -26.501217, z = -16.563267 },
    { x = -26.504532, z = -15.427269 },
    { x = -26.509769, z = -14.327281 },
    { x = -26.565643, z = -4.247434 },
    { x = -26.573967, z = -5.299402 },
    { x = -26.579763, z = -6.379386 },
    { x = -26.580465, z = -8.155381 },
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
    if csid == 217 or csid == 219 or csid == 229 or csid == 239 then
        xi.cs.questEvent(player, npc, 'outlands.the_opo_opo_and_i', csid, option)
    end
end

return entity
