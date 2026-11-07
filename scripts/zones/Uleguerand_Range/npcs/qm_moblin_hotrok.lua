-----------------------------------
-- Area: Uleguerand_Range
--  NPC: ??? (Trade Moblin Hotrok for Map of Uleguerand Range)
-- !pos -299 -62 -18
-- Involved in Quests: Over The Hills And Far Away
-----------------------------------
---@type TNpcEntity
local entity = {}

-- TODO: This logic needs to be verified, and conditions slimmed down.  Companion of Louverance is granted on completion of that path,
-- while True Companion is granted during the Ulmia path.

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.over_the_hills_and_far_away', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'sandoria.over_the_hills_and_far_away', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 10 then
        xi.cs.questEvent(player, npc, 'sandoria.over_the_hills_and_far_away', csid, option)
    end
end

return entity
