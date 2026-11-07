-----------------------------------
-- Area: Yhoator Jungle
--   NM: Kappa Bonze
-- Involved in Quest: True will
-----------------------------------
mixins = { require('scripts/mixins/job_special') }
-----------------------------------
---@type TMobEntity
local entity = {}

entity.onMobInitialize = function(mob)
    mob:setMobMod(xi.mobMod.IDLE_DESPAWN, 180)
end

entity.onMobSpawn = function(mob)
    mob:setMobMod(xi.mobMod.BASE_DAMAGE_MULTIPLIER, 150)
end

entity.onMobDeath = function(mob, player, optParams)
    xi.cs.quest(player, mob, 'outlands.true_will', 'onMobDeath')
end

return entity
