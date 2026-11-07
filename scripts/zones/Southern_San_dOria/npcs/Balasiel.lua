-----------------------------------
-- Area: Southern San d'Oria (230)
--  NPC: Balasiel
-- Starts and Finishes: A Squire's Test, A Squire's Test II, A Knight's Test, Methods Create Madness
-- !pos -136 -11 64 230
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- DRG AF3 cutscene, doesn't appear to have a follow up.
    xi.cs.quest(player, npc, 'sandoria.knight_stalker', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 63 then
        xi.cs.questEvent(player, npc, 'sandoria.knight_stalker', csid, option)
    end
end

--    player:startEvent(32690)     -- starlight celebration

return entity
