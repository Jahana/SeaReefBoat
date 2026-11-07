-----------------------------------
-- Area: Southern San d'Oria
--  NPC: Kipopo
-- Type: Leathercraft Synthesis Image Support
-- !pos -191.050 -2.15 12.285 230
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'crystalWar.say_it_with_a_handbag', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'crystalWar.say_it_with_a_handbag', 'onTrigger') then
        return
    end

    xi.crafting.oldImageSupportOnTrigger(player, npc)
end

entity.onEventFinish = function(player, csid, option, npc)
    if
        csid == 914 or
        csid == 912 or
        csid == 910 or
        (csid == 908 and option == 1)
    then
        xi.cs.questEvent(player, npc, 'crystalWar.say_it_with_a_handbag', csid, option)
    else
        xi.crafting.oldImageSupportOnEventFinish(player, csid, option, npc)
    end
end

return entity
