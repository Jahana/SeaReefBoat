-----------------------------------
-- Area: Yhoator Jungle
--   NM: Kappa Akuso
-- Involved in Quest: True will
-----------------------------------
mixins = { require('scripts/mixins/job_special') }
-----------------------------------
---@type TMobEntity
local entity = {}

entity.onMobInitialize = function(mob)
    mob:setMobMod(xi.mobMod.IDLE_DESPAWN, 180)
    mob:addImmunity(xi.immunity.DARK_SLEEP)
    mob:addImmunity(xi.immunity.LIGHT_SLEEP)
    mob:addImmunity(xi.immunity.PETRIFY)
    mob:addImmunity(xi.immunity.SILENCE)
    mob:addImmunity(xi.immunity.TERROR)
end

entity.onMobSpawn = function(mob)
    mob:setMobMod(xi.mobMod.CANNOT_GUARD, 1)
    mob:setMobMod(xi.mobMod.BASE_DAMAGE_MULTIPLIER, 250)
end

entity.onMobDeath = function(mob, player, optParams)
    xi.cs.quest(player, mob, 'outlands.true_will', 'onMobDeath')
end

return entity
