-----------------------------------
-- Area: Sea Serpent Grotto
--  NPC: Hurr the Betrayer
-- Type: Involved in the 'Sahagin Key Quest'
-- !pos 305.882 26.768 234.279 176
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    -- THE SAHAGIN'S KEY: 107 for the coins and the Norg Shell SahaginKeyItems names
    xi.cs.quest(player, npc, 'outlands.the_sahagins_key', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    -- THE SAHAGIN'S KEY: 105 or 106 (the coins asked for), else 104
    xi.cs.quest(player, npc, 'outlands.the_sahagins_key', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    -- THE SAHAGIN'S KEY: 107 gives the key
    if csid == 107 then
        xi.cs.questEvent(player, npc, 'outlands.the_sahagins_key', csid, option)
    end
end

return entity
