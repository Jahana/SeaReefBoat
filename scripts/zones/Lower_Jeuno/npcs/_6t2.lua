-----------------------------------
-- Area: Lower Jeuno
-- Door: Merchant's House
-- Starts & Finishes Quest: Save My Son
-- Optional Involvement in Quest: Chocobo's Wounds, Path of the Beastmaster
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- A New Dawn (BST AF3)
    xi.cs.quest(player, npc, 'jeuno.a_new_dawn', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 5 or csid == 4 or csid == 0 then
        xi.cs.questEvent(player, npc, 'jeuno.a_new_dawn', csid, option)
    end
end

return entity
