-----------------------------------
-- Area: Windurst Woods
--  NPC: Kororo
-- !pos -11.883 -2.750 5.508 241
-- Involved in Quests: A Greeting Cardian, Lost Chick
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.the_all_new_c_2000', 'onTrigger') then
        -- Might be Legendary Plan B, most likely Lost Chick related.
        -- only activates before LPB completes so leaving it in as is for now
        xi.cs.quest(player, npc, 'windurst.legendary_plan_b', 'onTrigger')
    end
end

return entity
