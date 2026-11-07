-----------------------------------
-- Area: Windurst Waters
--  NPC: Furakku-Norakku
-- Involved in Quests: Early Bird Catches the Bookworm, Chasing Tales, Class Reunion
-- !pos -19 -5 101 238
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- Class Reunion
    xi.cs.quest(player, npc, 'windurst.class_reunion', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 816 then
        xi.cs.questEvent(player, npc, 'windurst.class_reunion', csid, option)
    end
end

return entity
