-----------------------------------
-- Area: Kazham
--  NPC: Magriffon
-- Involved in Quest: Gullible's Travels, Even More Gullible's Travels,
-- Location: (I-7)
-----------------------------------
---@type TNpcEntity
local entity = {}

local pathNodes =
{
    { x = 60.600, y = -12.000, z = -33.913, wait = 3000 },
    { z = -38.151, wait = 3000 },
}

entity.onSpawn = function(npc)
    npc:initNpcAi()
    npc:setPos(xi.path.first(pathNodes))
    npc:pathThrough(pathNodes, xi.path.flag.PATROL)
end

entity.onTrade = function(player, npc, trade)
    if xi.cs.quest(player, npc, 'outlands.gullibles_travels', 'onTrade', trade) then
        return
    end

    xi.cs.quest(player, npc, 'outlands.even_more_gullibles_travels', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'outlands.gullibles_travels', 'onTrigger') then
        return
    end

    if xi.cs.quest(player, npc, 'outlands.even_more_gullibles_travels', 'onTrigger') then
        return
    end

    player:startEvent(143)
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 144 or csid == 146 then
        xi.cs.questEvent(player, npc, 'outlands.gullibles_travels', csid, option)
    elseif csid == 148 or csid == 150 or csid == 152 then
        xi.cs.questEvent(player, npc, 'outlands.even_more_gullibles_travels', csid, option)
    end
end

return entity
