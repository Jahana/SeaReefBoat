-----------------------------------
-- Area: The Eldieme Necropolis
--  Mob: Sturm
-- Involved in Quest: A New Dawn (BST AF3)
-----------------------------------
local ID = zones[xi.zone.THE_ELDIEME_NECROPOLIS]
-----------------------------------
---@type TMobEntity
local entity = {}

entity.onMobInitialize = function(mob)
    mob:setMobMod(xi.mobMod.IDLE_DESPAWN, 300)
    mob:addImmunity(xi.immunity.DARK_SLEEP)
    mob:addImmunity(xi.immunity.LIGHT_SLEEP)
end

entity.onMobSpawn = function(mob)
    mob:setMobMod(xi.mobMod.BASE_DAMAGE_MULTIPLIER, 250)
    mob:setMod(xi.mod.STORETP, 75) -- 8 hits to 1k tp
end

entity.onMobDeath = function(mob, player, optParams)
    -- a death with no killer has no player: the quest is skipped and the despawns below still run (a deliberate change
    -- from upstream, which read player:getQuestStatus on the nil player and raised: user ruling 2026-11-05 (kept bugs))
    if player then
        xi.cs.quest(player, mob, 'jeuno.a_new_dawn', 'onMobDeath')
    end

    if
        optParams.isKiller or
        optParams.noKiller
    then
        for i = ID.mob.TAIFUN, ID.mob.TROMBE do
            if GetMobByID(i):isSpawned() then
                DespawnMob(i)
            end
        end
    end
end

return entity
