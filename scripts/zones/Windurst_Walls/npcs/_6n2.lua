-----------------------------------
-- Area: Windurst Walls
-- Door: House of the Hero
-- Involved In Quest: Know One's Onions, Onion Rings, The Puppet Master, Class Reunion
-- !pos -26 -13 260 239
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- CLASS REUNION
    if xi.cs.quest(player, npc, 'windurst.class_reunion', 'onTrigger') then
        return
    end

    -- CARBUNCLE DEBACLE
    xi.cs.quest(player, npc, 'windurst.carbuncle_debacle', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    -- CLASS REUNION
    if csid == 413 then
        xi.cs.questEvent(player, npc, 'windurst.class_reunion', csid, option)

    -- CARBUNCLE DEBACLE
    elseif csid == 415 then
        xi.cs.questEvent(player, npc, 'windurst.carbuncle_debacle', csid, option)
    end
end

return entity
