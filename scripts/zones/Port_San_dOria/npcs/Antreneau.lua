-----------------------------------
-- Area: Port San d'Oria
--  NPC: Antreneau
-- !pos -71 -5 -39 232
-- Involved in Quests: A Taste For Meat, Over The Hills And Far Away
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    player:startEvent(532) -- What's this?  I don't need this.
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'sandoria.over_the_hills_and_far_away', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 725 then
        xi.cs.questEvent(player, npc, 'sandoria.over_the_hills_and_far_away', csid, option)
    end
end

return entity
