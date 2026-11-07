-----------------------------------
-- The Monstrosity unlock quest's city NPCs (scripts/globals/monstrosity/city.lua) before the quest is begun. The quest
-- itself is C# (src/XI.QuestContent/OtherAreas/Monstrosity.cs; tests/golden/flows/otherAreas.monstrosity*.flow).
-----------------------------------

describe('Monstrosity quest: the city NPCs (city.lua) before the quest is begun', function()
    ---@type CClientEntityPair
    local player

    before_each(function()
        xi.test.world:setSetting('main.ENABLE_MONSTROSITY', 1)
        -- Rumors from the West and Rhapsodies of Vana'diel play their own cutscenes on entering a city.
        xi.test.world:setSetting('main.ENABLE_SOA', 0)
        xi.test.world:setSetting('main.ENABLE_ROV', 0)
        player = xi.test.world:spawnPlayer({ zone = xi.zone.NORTHERN_SAN_DORIA })
    end)

    it('lets the Suspicious Elvaan chat before the Hume is spoken to', function()
        player.entities:gotoAndTrigger('Suspicious_Elvaan', { eventId = 881 })
    end)

    it('says nothing happens at a city Odyssean Passage without the ring', function()
        player.entities:gotoAndTrigger('Odyssean_Passage')

        assert(not player:isInEvent(), 'the Passage opened without the ring')
    end)
end)
