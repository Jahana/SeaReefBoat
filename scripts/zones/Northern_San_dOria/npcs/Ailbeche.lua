-----------------------------------
-- Area: Northern San d'Oria
--  NPC: Ailbeche
-- Starts and Finishes Quest: Father and Son, Sharpening the Sword, A Boy's Dream (start)
-- !pos 4 -1 24 231
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.a_boys_dream', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'sandoria.a_boys_dream', 'onTrigger') then
        return
    elseif
        player:hasKeyItem(xi.keyItem.KNIGHTS_CONFESSION) and
        player:getCharVar('UnderOathCS') == 6
    then
        player:startEvent(59) -- During Quest 'Under Oath' (he's going fishing in Jugner)
    elseif player:getCharVar('UnderOathCS') == 8 then
        player:startEvent(13) -- During Quest 'Under Oath' (After jugner CS)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    -- "A Boy's Dream"
    if csid == 41 or csid == 40 or csid == 15 or csid == 47 or csid == 25 then
        xi.cs.questEvent(player, npc, 'sandoria.a_boys_dream', csid, option)
    elseif csid == 59 then
        player:setCharVar('UnderOathCS', 7)
    end
end

return entity
