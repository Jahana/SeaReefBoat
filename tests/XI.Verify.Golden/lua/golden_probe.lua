-----------------------------------
-- Golden-value combat probe, shared by both sides of the parity harness:
--   * the C++ capture (capture/golden_capture_spec.lua run by xi_test.exe) writes what it emits to golden/combat.tsv;
--   * the C# check (XI.Verify.Golden) runs the same cases through the C# ZoneHost and compares.
-- Everything here goes through the entity bindings both servers expose, so the same file runs unchanged in both.
-- Random draws are replaced by a scripted fraction sequence (golden.withRng) so the random formulas are deterministic;
-- the C# side feeds its native ports the same sequence through a stub System.Random.
-----------------------------------
local golden = {}

golden.zone = 100 -- West Ronfaure

-- the mobs used as targets (spawned first when they are not up): Wild Rabbit, Orcish Fodder (WAR), Goblin Thug (THF),
-- Enchanted Bones (BLM, undead), Orcish Mesmerizer (BLM), Scarab Beetle (PLD), Orcish Grappler (MNK)
golden.mobIds = { 17186823, 17186886, 17187031, 17187084, 17186935, 17186885, 17186887 }

-- the scripted draw sequences: each draw takes the next fraction f (cycling) and maps [a, b] to a + floor(f * (b - a + 1))
golden.rngModes =
{
    low  = { 0 },
    high = { 0.999999 },
    mid  = { 0.5 },
    mix1 = { 0.999999, 0, 0.5, 0.5 },
    mix2 = { 0.999999, 0.999999, 0.25, 0 },
    mix3 = { 0.75, 0.2, 0.9, 0.6 },
}
golden.rngModeOrder = { 'low', 'high', 'mid', 'mix1', 'mix2', 'mix3' }

local function roundHalfAway(x)
    if x >= 0 then
        return math.floor(x + 0.5)
    end

    return -math.floor(-x + 0.5)
end

-- Runs fn with math.random / randomInt / randomFloat replaced by the fraction sequence. An empty integer range
-- (b <= a) answers a without a draw, as the C# RandomInt does.
golden.withRng = function(fractions, fn)
    local savedRandom, savedInt, savedFloat = math.random, math.randomInt, math.randomFloat
    local n = 0

    local function nextFraction()
        n = n + 1
        return fractions[((n - 1) % #fractions) + 1]
    end

    local function int(a, b)
        if b <= a then
            return a
        end

        local size = b - a + 1
        return a + math.min(math.floor(nextFraction() * size), size - 1)
    end

    math.randomInt = function(a, b)
        return int(roundHalfAway(a), roundHalfAway(b))
    end

    math.random = function(a, b)
        if a == nil then
            return nextFraction()
        elseif b == nil then
            return int(1, roundHalfAway(a))
        end

        return int(roundHalfAway(a), roundHalfAway(b))
    end

    math.randomFloat = function(a, b)
        if a >= b then
            return a
        end

        return a + nextFraction() * (b - a)
    end

    local ok, result = pcall(fn)
    math.random, math.randomInt, math.randomFloat = savedRandom, savedInt, savedFloat

    if not ok then
        error(result, 0)
    end

    return result
end

golden.fmt = function(v)
    if v == nil then
        return 'nil'
    elseif type(v) == 'boolean' then
        return v and 'true' or 'false'
    elseif type(v) == 'number' then
        if v == math.floor(v) and math.abs(v) < 2 ^ 52 then
            return string.format('%d', v)
        end

        return string.format('%.17g', v)
    end

    return tostring(v)
end

-- the upvalue named name of function fn (the files' local helpers), nil when absent
local function upvalue(fn, name)
    local i = 1
    while true do
        local n, v = debug.getupvalue(fn, i)
        if n == nil then
            return nil
        elseif n == name then
            return v
        end

        i = i + 1
    end
end

-----------------------------------
-- The chars: 1-8 one per race; 9+ chars with merits and job points raised once before any case (the capture raises
-- them in its own Mog House, the C# check with the char's Mog House flag set), each { race, merits = { { merit,
-- upgrades } }, jobPoints = { { job, { { job point, level } } } } }. A case runs on char c.char (default c.race).
-----------------------------------
golden.chars = { { race = 1 }, { race = 2 }, { race = 3 }, { race = 4 }, { race = 5 }, { race = 6 }, { race = 7 }, { race = 8 } }

local function meritChars()
    local mt, jp, j = xi.merit, xi.jp, xi.job
    return
    {
        -- 9: melee
        {
            race   = 2,
            merits =
            {
                { mt.MAX_HP, 5 }, { mt.MAX_MP, 3 }, { mt.STR, 4 }, { mt.DEX, 5 }, { mt.VIT, 2 }, { mt.AGI, 3 },
                { mt.HAND_TO_HAND_SKILL, 6 }, { mt.DAGGER_SKILL, 8 }, { mt.SWORD_SKILL, 4 }, { mt.KATANA_SKILL, 8 },
                { mt.GREAT_AXE_SKILL, 3 }, { mt.EVASION_SKILL, 8 }, { mt.PARRYING_SKILL, 5 }, { mt.SHIELD_SKILL, 4 },
                { mt.CRIT_HIT_RATE, 5 }, { mt.ENEMY_CRIT_RATE, 3 },
                { mt.DOUBLE_ATTACK_RATE, 5 }, { mt.TRIPLE_ATTACK_RATE, 5 }, { mt.STORE_TP_EFFECT, 5 }, { mt.SUBTLE_BLOW_EFFECT, 5 },
                { mt.KICK_ATTACK_RATE, 5 }, { mt.ZANSHIN_ATTACK_RATE, 4 },
                { mt.AMBUSH, 5 }, { mt.INNIN_EFFECT, 5 }, { mt.YONIN_EFFECT, 3 }, { mt.IKISHOTEN, 5 }, { mt.CLOSED_POSITION, 5 },
                { mt.NIN_MAGIC_ACCURACY, 2 },
                -- the weapon skill merits past their unlock (the golden 3 weapon skill cases)
                { mt.EXENTERATOR, 5 }, { mt.RUINATOR, 4 }, { mt.REQUIESCAT, 3 },
            },
            jobPoints =
            {
                { j.NIN, { { jp.INNIN_EFFECT, 10 }, { jp.YONIN_EFFECT, 7 }, { jp.NINJITSU_ACC_BONUS, 6 } } },
                { j.WAR, { { jp.MIGHTY_STRIKES_EFFECT, 5 }, { jp.BERSERK_EFFECT, 4 }, { jp.RESTRAINT_EFFECT, 3 } } },
                { j.THF, { { jp.SNEAK_ATTACK_EFFECT, 6 }, { jp.TRICK_ATTACK_EFFECT, 4 } } },
            },
        },
        -- 10: magic
        {
            race   = 6,
            merits =
            {
                { mt.MAX_MP, 6 }, { mt.INT, 5 }, { mt.MND, 5 }, { mt.ELEMENTAL_MAGIC_SKILL, 8 }, { mt.HEALING_MAGIC_SKILL, 8 },
                { mt.ENFEEBLING_MAGIC_SKILL, 8 }, { mt.DIVINE_MAGIC_SKILL, 4 }, { mt.CLUB_SKILL, 4 },
                { mt.FIRE_MAGIC_POTENCY, 5 }, { mt.EARTH_MAGIC_ACCURACY, 5 }, { mt.FIRE_MAGIC_ACCURACY, 3 },
                { mt.BANISH_EFFECT, 5 }, { mt.ELEMENTAL_MAGIC_ACCURACY, 5 }, { mt.ANCIENT_MAGIC_ATK_BONUS, 3 },
                { mt.MAGIC_ACCURACY, 5 }, { mt.ACCURACY, 3 }, { mt.ENFEEBLING_MAGIC_DURATION, 2 },
            },
            jobPoints =
            {
                { j.WHM, { { jp.WHM_MAGIC_ACC_BONUS, 10 } } },
                { j.BLM, { { jp.BLM_MAGIC_ACC_BONUS, 10 }, { jp.MAGIC_DMG_BONUS, 5 }, { jp.MANAFONT_EFFECT, 3 } } },
                { j.RDM, { { jp.RDM_MAGIC_ACC_BONUS, 5 } } },
            },
        },
        -- 11: pets
        {
            race   = 7,
            merits =
            {
                { mt.SUMMONING_MAGIC_SKILL, 8 }, { mt.AVATAR_PHYSICAL_ACCURACY, 5 }, { mt.AVATAR_PHYSICAL_ATTACK, 5 },
                { mt.AVATAR_MAGICAL_ACCURACY, 3 }, { mt.AVATAR_MAGICAL_ATTACK, 3 }, { mt.AUTOMATON_SKILLS, 5 },
                { mt.BEAST_AFFINITY, 2 }, { mt.MAX_HP, 4 }, { mt.CHR, 3 },
            },
            jobPoints =
            {
                {
                    j.SMN,
                    {
                        { jp.SUMMON_ACC_BONUS, 10 }, { jp.SUMMON_MAGIC_ACC_BONUS, 5 }, { jp.SUMMON_PHYS_ATK_BONUS, 6 },
                        { jp.SUMMON_MAGIC_DMG_BONUS, 4 }, { jp.BLOOD_PACT_DMG_BONUS, 3 },
                    },
                },
                { j.DRG, { { jp.WYVERN_MAX_HP_BONUS, 5 } } },
                { j.PUP, { { jp.AUTOMATON_HP_MP_BONUS, 4 } } },
            },
        },
    }
end

for _, spec in ipairs(meritChars()) do
    table.insert(golden.chars, spec)
end

-- Raises a char's merits and job points (0x0BE edit / raise, 0x0BF spend) while it is in a Mog House; send(opcode,
-- size, bytes) sends a client packet with bytes = { [offset] = byte }.
golden.setupChar = function(p, spec, send)
    for _, m in ipairs(spec.merits or {}) do
        local param2 = bit.rshift(m[1], 1)
        for _ = 1, m[2] do
            p:setMerits(30)
            send(0x0BE, 12, { [4] = 3, [5] = 1, [6] = bit.band(param2, 0xFF), [7] = bit.rshift(param2, 8) })
        end
    end

    for _, entry in ipairs(spec.jobPoints or {}) do
        p:changeJob(entry[1])
        p:setLevel(99)
        for _, jpEntry in ipairs(entry[2]) do
            for _ = 1, jpEntry[2] do
                p:setJobPoints(500)
                send(0x0BF, 8, { [4] = bit.band(jpEntry[1], 0xFF), [5] = bit.rshift(jpEntry[1], 8) })
            end
        end
    end

    p:setMerits(0)
    p:changeJob(xi.job.WAR)
    p:setLevel(1)
end

-----------------------------------
-- The case table: every job / level config against several targets and modifier sets.
-----------------------------------
local function weaponFor(job, level)
    local j = xi.job
    if job == j.WAR then
        return level >= 20 and 16643 or 16535 -- battleaxe / bronze sword
    elseif job == j.MNK then
        return 16385 -- cesti
    elseif job == j.THF then
        return level >= 70 and 16480 or 16465 -- thief's knife / bronze knife
    elseif job == j.PLD then
        return level >= 75 and 17744 or 16535 -- brave blade / bronze sword
    elseif job == j.DRK then
        return level >= 40 and 16775 or 16768 -- mythril scythe / bronze zaghnal
    elseif job == j.BLM then
        return 17088 -- ash staff
    elseif job == j.WHM then
        return 17024 -- ash club
    elseif job == j.NIN then
        return 16896 -- kunai
    elseif job == j.DRG then
        return level >= 24 and 16835 or nil -- spear / bare handed
    elseif job == j.RNG or job == j.BRD or job == j.COR or job == j.DNC then
        return 16465 -- bronze knife
    elseif job == j.BST or job == j.RUN then
        return level >= 20 and 16643 or nil -- battleaxe
    elseif job == j.SAM then
        return level >= 24 and 16835 or nil -- spear
    elseif job == j.SMN then
        return 17088 -- ash staff
    elseif job == j.BLU then
        return 16535 -- bronze sword
    elseif job == j.PUP then
        return 16405 -- cat baghnakhs
    elseif job == j.SCH or job == j.GEO then
        return 17024 -- ash club
    end

    return nil
end

local function rangedFor(job, level)
    if job == xi.job.RNG then
        return 17152 -- shortbow
    elseif job == xi.job.WAR and level >= 10 then
        return 17216 -- light crossbow
    end

    return nil
end

-- the off hand: a second dagger for dual wield (NIN, THF/NIN), a shield (PLD, WHM)
local function subFor(job, level)
    if job == xi.job.NIN and level >= 10 then
        return 16465 -- bronze knife
    elseif job == xi.job.THF and level >= 30 then
        return 16448 -- bronze dagger
    elseif job == xi.job.PLD or job == xi.job.WHM then
        return 12289 -- lauan shield
    end

    return nil
end

local function ammoFor(job, level)
    if job == xi.job.RNG then
        return 17318 -- wooden arrow
    elseif job == xi.job.WAR and level >= 10 then
        return 17336 -- crossbow bolt
    end

    return nil
end

local subJobs =
{
    [xi.job.WAR] = xi.job.NIN,
    [xi.job.THF] = xi.job.NIN,
    [xi.job.PLD] = xi.job.WAR,
    [xi.job.BLM] = xi.job.WHM,
    [xi.job.WHM] = xi.job.BLM,
    [xi.job.DRK] = xi.job.SAM,
}

local variants =
{
    [0] = { player = {}, mob = {} },
    [1] =
    {
        player = { STR = 30, DEX = 20, ATT = 60, ACC = 40, STORETP = 15, CRIT_DMG_INCREASE = 10, CRITHITRATE = 7, DAMAGE_LIMIT = 20, HASTE_GEAR = 1500, RATT = 25, RACC = 15 },
        mob    = { DEF = 50, EVA = 30, VIT = 20, AGI = 15, CRIT_DEF_BONUS = 4, INHIBIT_TP = 20, SUBTLE_BLOW = 10 },
    },
    [2] =
    {
        player = { DMGPHYS = -2500, DMG = -1000, DMGPHYS_II = -1000, DMGRANGE = -1500, RECEIVED_DAMAGE_CAP = 300, MACC = 30, MATT = 20, INT = 25, MND = 25, VIT = 12, CURE_POTENCY = 30, CURE_POTENCY_II = 10, ENMITY = 10, SUBTLE_BLOW = 25, MEVA = 20, DAMAGE_LIMITP = 15 },
        mob    = { MEVA = 40, INT = 10, MND = 10, STORETP = 20, ATT = 40, ACC = 30, DMGPHYS = -1000, UDMGPHYS = 500 },
    },
    [3] =
    {
        player = { STR = -15, ATT = -40, ACC = -60, EVA = 60, DEF = 80, AGI = 30 },
        mob    = { STR = 40, DEX = 25, CRITICAL_HIT_EVASION = 5, DMG = -500 },
    },
    [4] =
    {
        player = { MATT = 25, MACC = 15, ENH_MAGIC_DURATION = 10, ENF_MAG_POTENCY = 5, ENF_MAG_DURATION = 10, SONG_DURATION_BONUS = 20, ALL_SONGS_EFFECT = 1 },
        mob    = { DMG_AOE = -2500, MDEF = 15, MEVA = 30, UDMGMAGIC = -500 },
    },
    -- the cast / recast and magic damage taken modifiers
    [5] =
    {
        player = { FASTCAST = 35, UFASTCAST = 10, CURE_CAST_TIME = 15, ELEMENTAL_CELERITY = 10, SONG_SPELLCASTING_TIME = 20, HASTE_MAGIC = 2000,
                   HASTE_GEAR = 1000, BLACK_MAGIC_CAST = -10, WHITE_MAGIC_CAST = -10, BLACK_MAGIC_RECAST = -10, WHITE_MAGIC_RECAST = -15,
                   ELEMENTAL_MAGIC_RECAST = -5, ENHANCING_MAGIC_RECAST = 10, INSPIRATION_FAST_CAST = 6, SONG_RECAST_DELAY = 2,
                   DMGMAGIC = -2000, DMGMAGIC_II = -1000, UDMGMAGIC = 1000, DMG = -1500 },
        mob    = { DMGMAGIC = -1000, DMG = -3000, DMGMAGIC_II = -2500, UDMGMAGIC = -500, RECEIVED_DAMAGE_CAP = 400 },
    },
}

golden.cases = function()
    local list = {}
    local jobs = { xi.job.WAR, xi.job.MNK, xi.job.THF, xi.job.PLD, xi.job.DRK, xi.job.BLM, xi.job.WHM, xi.job.NIN, xi.job.DRG, xi.job.RNG }
    local levels = { 1, 10, 30, 50, 75, 99 }

    for ji, job in ipairs(jobs) do
        local race = ((ji - 1) % 8) + 1
        for li, level in ipairs(levels) do
            local sjob = level >= 10 and subJobs[job] or nil
            local base =
            {
                race   = race,
                job    = job,
                level  = level,
                sjob   = sjob,
                slevel = sjob and math.floor(level / 2) or nil,
                main   = weaponFor(job, level),
                sub    = subFor(job, level),
                ranged = rangedFor(job, level),
                ammo   = ammoFor(job, level),
            }
            local targets =
            {
                { mob = golden.mobIds[((ji + li) % #golden.mobIds) + 1], mlvl = level, variant = 0 },
                { mob = golden.mobIds[((ji + li + 1) % #golden.mobIds) + 1], mlvl = math.min(level + 8, 99), variant = 1 },
                { mob = golden.mobIds[((ji + li + 2) % #golden.mobIds) + 1], mlvl = math.max(level - 6, 1), variant = 2 },
                { mob = golden.mobIds[((ji + li + 3) % #golden.mobIds) + 1], mlvl = math.min(level + 3, 99), variant = 3 },
            }

            for ti, t in ipairs(targets) do
                local c = {}
                for k, v in pairs(base) do
                    c[k] = v
                end

                c.mob     = t.mob
                c.mlvl    = t.mlvl
                c.variant = t.variant
                c.name    = string.format('j%02d_l%02d_t%d', job, level, ti)
                table.insert(list, c)
            end
        end
    end

    -- the other jobs: stats, skills and formulas at four levels against two targets
    local moreJobs =
    {
        xi.job.BST, xi.job.BRD, xi.job.SAM, xi.job.SMN, xi.job.BLU, xi.job.COR, xi.job.PUP, xi.job.DNC, xi.job.SCH,
        xi.job.GEO, xi.job.RUN,
    }

    for ji, job in ipairs(moreJobs) do
        for li, level in ipairs({ 1, 30, 75, 99 }) do
            for ti, variant in ipairs({ 0, 1 }) do
                table.insert(list,
                {
                    name    = string.format('j%02d_l%02d_t%d', job, level, ti),
                    race    = ((ji + li) % 8) + 1,
                    job     = job,
                    level   = level,
                    sjob    = level >= 10 and xi.job.WAR or nil,
                    slevel  = level >= 10 and math.floor(level / 2) or nil,
                    main    = weaponFor(job, level),
                    mob     = golden.mobIds[((ji + li + ti) % #golden.mobIds) + 1],
                    mlvl    = variant == 0 and level or math.min(level + 8, 99),
                    variant = variant,
                })
            end
        end
    end

    -- the hit-rate / pDIF modifiers: Signet (an engaged char against an even match or weaker), Innin from behind,
    -- Yonin facing the target
    local fxJobs = { xi.job.WAR, xi.job.NIN, xi.job.THF, xi.job.MNK, xi.job.PLD }
    local fxList =
    {
        { fx = 'signet', pos = 'front', engage = true, dl = 0 },
        { fx = 'signet', pos = 'front', engage = true, dl = -8 },
        { fx = 'signet', pos = 'front', engage = true, dl = 6 },
        { fx = 'signet', pos = 'front', engage = false, dl = 0 },
        { fx = 'innin', pos = 'back', dl = 0 },
        { fx = 'innin', pos = 'front', dl = 0 },
        { fx = 'yonin', pos = 'front', dl = 0 },
        { fx = 'yonin', pos = 'back', dl = 0 },
    }

    for ji, job in ipairs(fxJobs) do
        for li, level in ipairs({ 10, 50, 75 }) do
            for fi, fx in ipairs(fxList) do
                local sjob = subJobs[job]
                table.insert(list,
                {
                    name    = string.format('fx_j%02d_l%02d_%d_%s_%s', job, level, fi, fx.fx, fx.pos),
                    race    = ((ji + fi) % 8) + 1,
                    job     = job,
                    level   = level,
                    sjob    = sjob,
                    slevel  = sjob and math.floor(level / 2) or nil,
                    main    = weaponFor(job, level),
                    mob     = golden.mobIds[((ji + li + fi) % #golden.mobIds) + 1],
                    mlvl    = math.max(level + fx.dl, 1),
                    variant = 0,
                    fx      = fx.fx,
                    pos     = fx.pos,
                    engage  = fx.engage,
                })
            end
        end
    end

    -- pets: an avatar / spirit, a wyvern, a jug pet (its level range pinned by JUG_LEVEL_RANGE), an automaton and a
    -- luopan as attacker and target; the low-level targets bring the avatar level-difference clamp in
    local p = xi.petId
    local petJobs =
    {
        { job = xi.job.SMN, levels = { 10, 50, 75, 99 }, pets = { p.CARBUNCLE, p.IFRIT, p.FIRE_SPIRIT, p.DIABOLOS } },
        { job = xi.job.DRG, levels = { 10, 50, 75, 99 }, pets = { p.WYVERN } },
        { job = xi.job.BST, levels = { 30, 75, 99 }, pets = { p.SHEEP_FAMILIAR, p.COURIER_CARRIE, p.FATSO_FARGANN } },
        { job = xi.job.PUP, levels = { 10, 50, 75, 99 }, pets = { p.AUTOMATON } },
        { job = xi.job.GEO, levels = { 10, 75, 99 }, pets = { p.LUOPAN } },
    }

    for ji, pj in ipairs(petJobs) do
        for li, level in ipairs(pj.levels) do
            for pi, petId in ipairs(pj.pets) do
                for ti, dl in ipairs({ 0, -45 }) do
                    local n = ji + li + pi + ti
                    table.insert(list,
                    {
                        name    = string.format('pet_j%02d_l%02d_p%02d_t%d', pj.job, level, petId, ti),
                        race    = (n % 8) + 1,
                        job     = pj.job,
                        level   = level,
                        sjob    = level >= 10 and xi.job.WAR or nil,
                        slevel  = level >= 10 and math.floor(level / 2) or nil,
                        main    = weaponFor(pj.job, level),
                        mob     = golden.mobIds[(n % #golden.mobIds) + 1],
                        mlvl    = math.max(level + dl, 1),
                        variant = ti == 1 and 0 or 1,
                        pet     = petId,
                    })
                end
            end
        end
    end

    -- the merit / job point chars: their jobs at 75 and 99 (and 50, under the level caps), in front of and behind the
    -- target, with Innin / Yonin up for the ninja
    local meritJobs =
    {
        { char = 9, job = xi.job.NIN, sjob = xi.job.WAR, fx = { 'innin', 'yonin' } },
        { char = 9, job = xi.job.THF, sjob = xi.job.NIN },
        { char = 9, job = xi.job.WAR, sjob = xi.job.NIN },
        { char = 9, job = xi.job.SAM, sjob = xi.job.WAR },
        { char = 9, job = xi.job.DNC, sjob = xi.job.NIN },
        { char = 9, job = xi.job.MNK, sjob = xi.job.WAR },
        { char = 10, job = xi.job.BLM, sjob = xi.job.WHM },
        { char = 10, job = xi.job.WHM, sjob = xi.job.BLM },
        { char = 10, job = xi.job.RDM, sjob = xi.job.WHM },
        { char = 11, job = xi.job.SMN, sjob = xi.job.WHM, pet = p.GARUDA },
        { char = 11, job = xi.job.DRG, sjob = xi.job.WAR, pet = p.WYVERN },
        { char = 11, job = xi.job.BST, sjob = xi.job.WAR, pet = p.FATSO_FARGANN },
        { char = 11, job = xi.job.PUP, sjob = xi.job.WAR, pet = p.AUTOMATON },
    }

    for mi, mj in ipairs(meritJobs) do
        for li, level in ipairs({ 50, 75, 99 }) do
            for pi, pos in ipairs({ 'front', 'back' }) do
                local fx = mj.fx and mj.fx[pi] or nil
                table.insert(list,
                {
                    name    = string.format('merit_c%02d_j%02d_l%02d_%s', mj.char, mj.job, level, pos),
                    char    = mj.char,
                    race    = golden.chars[mj.char].race,
                    job     = mj.job,
                    level   = level,
                    sjob    = mj.sjob,
                    slevel  = math.floor(level / 2),
                    main    = weaponFor(mj.job, level),
                    sub     = subFor(mj.job, level),
                    mob     = golden.mobIds[((mi + li + pi) % #golden.mobIds) + 1],
                    mlvl    = level + (pi == 1 and 0 or 3),
                    variant = 0,
                    merit   = true,
                    fx      = fx,
                    pos     = pos,
                    pet     = mj.pet,
                })
            end
        end
    end

    -- cure enmity through another char: a healer cures a char the mob hates (or one it does not), at levels below,
    -- at and above the healer's, with the healer's enmity modifiers in the variants
    local cureList =
    {
        { healer = 1, target = 2, hl = 75, tl = 75, variant = 0 },
        { healer = 1, target = 2, hl = 75, tl = 30, variant = 0 },
        { healer = 3, target = 4, hl = 30, tl = 75, variant = 2 },
        { healer = 5, target = 6, hl = 99, tl = 99, variant = 2 },
        { healer = 7, target = 8, hl = 10, tl = 1, variant = 0 },
        { healer = 6, target = 1, hl = 50, tl = 52, variant = 1 },
    }

    -- job abilities (their own Lua) and what they change
    local jaList =
    {
        { job = xi.job.WAR, sjob = xi.job.NIN, abilities = { 'berserk', 'warcry', 'aggressor', 'defender', 'mighty', 'restraint' } },
        { job = xi.job.MNK, sjob = xi.job.WAR, abilities = { 'footwork', 'counterstance', 'focus', 'dodge', 'berserk' } },
        { job = xi.job.SAM, sjob = xi.job.WAR, abilities = { 'hasso', 'seigan', 'thirdeye', 'meikyo', 'warcry' } },
        { job = xi.job.WAR, sjob = xi.job.SAM, abilities = { 'hasso', 'seigan', 'meikyo' } },
        { job = xi.job.NIN, sjob = xi.job.WAR, abilities = { 'yonin', 'innin', 'utsusemi1', 'utsusemi2' }, pos = 'back' },
        { job = xi.job.WAR, sjob = xi.job.NIN, abilities = { 'utsusemi1', 'utsusemi2' } },
        { job = xi.job.DRK, sjob = xi.job.SAM, abilities = { 'lastresort', 'arcanecircle', 'hasso' } },
    }

    for ji, ja in ipairs(jaList) do
        for li, level in ipairs({ 30, 75, 99 }) do
            local main = weaponFor(ja.job, level)
            if ja.sjob == xi.job.SAM or ja.job == xi.job.SAM then
                main = 16835 -- spear: two-handed, for Hasso / Seigan
            end

            table.insert(list,
            {
                name      = string.format('ja%d_j%02d_s%02d_l%02d', ji, ja.job, ja.sjob, level),
                race      = ((ji + li) % 8) + 1,
                job       = ja.job,
                level     = level,
                sjob      = ja.sjob,
                slevel    = math.floor(level / 2),
                main      = main,
                sub       = subFor(ja.job, level),
                mob       = golden.mobIds[((ji + li) % #golden.mobIds) + 1],
                mlvl      = level,
                variant   = li == 2 and 1 or 0,
                abilities = ja.abilities,
                pos       = ja.pos or 'front',
            })
        end
    end

    -- the abilities with the job points behind them (Hasso, Seigan, Dodge, Focus) on the merit char
    table.insert(list,
    {
        name      = 'ja_c09_sam',
        char      = 9,
        race      = golden.chars[9].race,
        job       = xi.job.SAM,
        level     = 99,
        sjob      = xi.job.WAR,
        slevel    = 49,
        main      = 16835,
        mob       = golden.mobIds[3],
        mlvl      = 99,
        variant   = 0,
        abilities = { 'hasso', 'seigan', 'meikyo' },
        pos       = 'front',
    })

    -- spells: their own Lua (damage, helix, enfeebling, songs, enhancing) on the target or the caster
    local spellList =
    {
        { job = xi.job.BLM, sjob = xi.job.RDM, spells = { 'stone', 'fire2', 'stonega', 'poison', 'blind', 'gravity', 'bind', 'burn' } },
        { job = xi.job.WHM, sjob = xi.job.BLM, spells = { 'banish', 'silence', 'paralyze', 'slow', 'protect', 'shell', 'barfire', 'regen', 'haste', 'stoneskin', 'blink' } },
        { job = xi.job.RDM, sjob = xi.job.WHM, spells = { 'stone', 'sleep', 'gravity', 'slow2', 'paralyze', 'distract', 'phalanx', 'refresh', 'enfire', 'haste' } },
        { job = xi.job.SCH, sjob = xi.job.RDM, spells = { 'geohelix', 'luminohelix', 'stone', 'protect', 'regen' } },
        { job = xi.job.NIN, sjob = xi.job.WAR, spells = { 'katon', 'hojo', 'kurayami' } },
        { job = xi.job.BRD, sjob = xi.job.WHM, spells = { 'requiem', 'lullaby', 'minuet', 'minuet4', 'madrigal', 'paeon', 'minne', 'march' } },
    }

    for si, sp in ipairs(spellList) do
        for li, level in ipairs({ 30, 75, 99 }) do
            for vi, variant in ipairs({ 0, 4 }) do
                table.insert(list,
                {
                    name    = string.format('spell_j%02d_l%02d_v%d', sp.job, level, variant),
                    race    = ((si + li + vi) % 8) + 1,
                    job     = sp.job,
                    level   = level,
                    sjob    = sp.sjob,
                    slevel  = math.floor(level / 2),
                    main    = weaponFor(sp.job, level),
                    mob     = golden.mobIds[((si + li + vi) % #golden.mobIds) + 1],
                    mlvl    = level + (vi == 1 and 0 or 5),
                    variant = variant,
                    spells  = sp.spells,
                })
            end
        end
    end

    -- the magic merit / job point char's spells
    for li, level in ipairs({ 75, 99 }) do
        for _, sp in ipairs({ { xi.job.BLM, xi.job.WHM, { 'stone', 'fire2', 'burn', 'poison' } }, { xi.job.RDM, xi.job.WHM, { 'slow2', 'paralyze', 'gravity', 'refresh' } } }) do
            table.insert(list,
            {
                name    = string.format('spell_c10_j%02d_l%02d', sp[1], level),
                char    = 10,
                race    = golden.chars[10].race,
                job     = sp[1],
                level   = level,
                sjob    = sp[2],
                slevel  = math.floor(level / 2),
                main    = weaponFor(sp[1], level),
                mob     = golden.mobIds[li + 2],
                mlvl    = level,
                variant = 0,
                spells  = sp[3],
            })
        end
    end

    for ci, cc in ipairs(cureList) do
        table.insert(list,
        {
            name    = string.format('cure_%d', ci),
            race    = cc.healer,
            other   = cc.target,
            job     = xi.job.WHM,
            level   = cc.hl,
            olevel  = cc.tl,
            sjob    = cc.hl >= 10 and xi.job.BLM or nil,
            slevel  = cc.hl >= 10 and math.floor(cc.hl / 2) or nil,
            main    = weaponFor(xi.job.WHM, cc.hl),
            mob     = golden.mobIds[(ci % #golden.mobIds) + 1],
            mlvl    = cc.tl,
            variant = cc.variant,
            cure    = true,
        })
    end

    -- weapon skill damage taken both ways, with each job's weapons (two daggers, a bow, fists, a two-handed spear)
    local wsJobs = { xi.job.WAR, xi.job.THF, xi.job.RNG, xi.job.MNK, xi.job.SAM, xi.job.NIN }
    for ji, job in ipairs(wsJobs) do
        for li, level in ipairs({ 10, 50, 75, 99 }) do
            local sjob = subJobs[job] or xi.job.WAR
            table.insert(list,
            {
                name    = string.format('ws_j%02d_l%02d', job, level),
                race    = ((ji + li) % 8) + 1,
                job     = job,
                level   = level,
                sjob    = sjob,
                slevel  = math.floor(level / 2),
                main    = weaponFor(job, level),
                sub     = subFor(job, level),
                ranged  = rangedFor(job, level),
                ammo    = ammoFor(job, level),
                mob     = golden.mobIds[((ji + li) % #golden.mobIds) + 1],
                mlvl    = level + (li % 2 == 0 and 4 or -2),
                variant = li % 2,
                ws      = true,
            })
        end
    end

    -- golden 3 (golden_hooks, the hooks xi_test gets from capture/xi_test_hooks.patch): the native auto-attack swing
    -- with Sneak / Trick Attack, GetCritHitRate, the native hit rate and MagicDmgTaken
    local nativeJobs =
    {
        { job = xi.job.THF, sjob = xi.job.NIN, levels = { 30, 75, 99 }, fx = { 'none', 'none', 'sa', 'sa', 'sadoubt', 'ta', 'mighty' } },
        { job = xi.job.WAR, sjob = xi.job.NIN, levels = { 50, 99 }, fx = { 'none', 'none', 'mighty', 'sa' } },
        { job = xi.job.MNK, sjob = xi.job.WAR, levels = { 30, 75 }, fx = { 'none', 'none', 'mighty' } },
        { job = xi.job.NIN, sjob = xi.job.WAR, levels = { 50, 75 }, fx = { 'none', 'innin', 'yonin' } },
        { job = xi.job.DRK, sjob = xi.job.SAM, levels = { 75, 99 }, fx = { 'none', 'none' } },
        { job = xi.job.PLD, sjob = xi.job.WAR, levels = { 75 }, fx = { 'none', 'none' } },
        { job = xi.job.RNG, sjob = xi.job.WAR, levels = { 50 }, fx = { 'none' } },
    }

    for ji, nj in ipairs(nativeJobs) do
        for li, level in ipairs(nj.levels) do
            for fi, fx in ipairs(nj.fx) do
                -- odd fx entries stand behind the target, even ones in front (Innin and Trick Attack behind, Yonin in
                -- front)
                local pos = fi % 2 == 0 and 'front' or 'back'
                if fx == 'innin' or fx == 'ta' then
                    pos = 'back'
                elseif fx == 'yonin' then
                    pos = 'front'
                end

                local n = ji + li + fi
                table.insert(list,
                {
                    name    = string.format('nat_j%02d_l%02d_%d_%s_%s', nj.job, level, fi, fx, pos),
                    race    = (n % 8) + 1,
                    job     = nj.job,
                    level   = level,
                    sjob    = nj.sjob,
                    slevel  = math.floor(level / 2),
                    main    = weaponFor(nj.job, level),
                    sub     = subFor(nj.job, level),
                    ranged  = rangedFor(nj.job, level),
                    ammo    = ammoFor(nj.job, level),
                    mob     = golden.mobIds[(n % #golden.mobIds) + 1],
                    mlvl    = math.max(level + (fi % 3) * 2 - 2, 1),
                    variant = ({ 0, 1, 2, 3, 5 })[(n % 5) + 1],
                    pos     = pos,
                    fx      = (fx == 'innin' or fx == 'yonin') and fx or nil,
                    engage  = true,
                    native  = fx,
                })
            end
        end
    end

    -- cast and recast times (CalculateSpellCastTime / CalculateSpellRecastTime) under the casting effects
    local castJobs =
    {
        { job = xi.job.BLM, sjob = xi.job.RDM, spells = { 'STONE', 'FIRE_II', 'STONEGA', 'POISON', 'SLEEP', 'DRAIN', 'BIO' }, fx = { 'none', 'hasso', 'darkarts' } },
        { job = xi.job.WHM, sjob = xi.job.BLM, spells = { 'CURE', 'CURE_III', 'BANISH', 'PROTECT', 'SLOW', 'PARALYNA', 'HASTE' }, fx = { 'none', 'lightarts', 'seigan' } },
        { job = xi.job.RDM, sjob = xi.job.WHM, spells = { 'STONE', 'PARALYZE', 'REFRESH', 'PHALANX', 'CURE' }, fx = { 'none', 'composure' } },
        { job = xi.job.SCH, sjob = xi.job.RDM, spells = { 'STONE', 'GEOHELIX', 'CURE', 'PROTECT' }, fx = { 'none', 'alacrity', 'celerity', 'darkarts', 'lightarts' } },
        { job = xi.job.BRD, sjob = xi.job.WHM, spells = { 'FOE_REQUIEM', 'FOE_LULLABY', 'VALOR_MINUET', 'ADVANCING_MARCH', 'MAGIC_FINALE', 'CURE' }, fx = { 'none', 'nightingale', 'troubadour', 'pianissimo' } },
        { job = xi.job.NIN, sjob = xi.job.WAR, spells = { 'KATON_ICHI', 'UTSUSEMI_ICHI', 'HOJO_ICHI' }, fx = { 'none' } },
        { job = xi.job.SMN, sjob = xi.job.WHM, spells = { 'CARBUNCLE', 'IFRIT', 'FIRE_SPIRIT', 'CURE' }, fx = { 'none' } },
        { job = xi.job.GEO, sjob = xi.job.WHM, spells = { 'GEO_REGEN', 'INDI_REGEN', 'STONE' }, fx = { 'none' } },
    }

    for ji, cj in ipairs(castJobs) do
        for li, level in ipairs({ 50, 99 }) do
            for fi, fx in ipairs(cj.fx) do
                for vi, variant in ipairs({ 0, 5 }) do
                    local n = ji + li + fi + vi
                    table.insert(list,
                    {
                        name    = string.format('cast_j%02d_l%02d_%s_v%d', cj.job, level, fx, variant),
                        race    = (n % 8) + 1,
                        job     = cj.job,
                        level   = level,
                        sjob    = cj.sjob,
                        slevel  = math.floor(level / 2),
                        main    = cj.job == xi.job.SCH and 16835 or weaponFor(cj.job, level),
                        mob     = golden.mobIds[(n % #golden.mobIds) + 1],
                        mlvl    = level,
                        variant = variant,
                        cast    = cj.spells,
                        castFx  = fx,
                    })
                end
            end
        end
    end

    -- area spells with the target count the cast would find: other mobs moved next to the target
    for li, level in ipairs({ 50, 75, 99 }) do
        for ni, extra in ipairs({ 0, 1, 2 }) do
            table.insert(list,
            {
                name    = string.format('aoe_j%02d_l%02d_n%d', xi.job.BLM, level, extra),
                race    = ((li + ni) % 8) + 1,
                job     = xi.job.BLM,
                level   = level,
                sjob    = xi.job.RDM,
                slevel  = math.floor(level / 2),
                main    = weaponFor(xi.job.BLM, level),
                mob     = golden.mobIds[1],
                mlvl    = level,
                variant = ni == 3 and 4 or 0,
                aoe     = extra,
                spells  = { 'stonega', 'fire2', 'stone' },
            })
        end
    end

    -- blood pacts: the pet ability scripts (mobPhysicalMove) with a real pet skill
    local bpList =
    {
        { pet = p.CARBUNCLE, skills = { 513 } },  -- poison nails
        { pet = p.IFRIT, skills = { 544, 547 } }, -- punch, double punch
        { pet = p.DIABOLOS, skills = { 656 } },   -- camisado
        { pet = p.GARUDA, skills = { 592 } },     -- claw
    }

    for bi, bp in ipairs(bpList) do
        for li, level in ipairs({ 50, 75, 99 }) do
            local n = bi + li
            table.insert(list,
            {
                name     = string.format('bp_j%02d_l%02d_p%02d', xi.job.SMN, level, bp.pet),
                race     = (n % 8) + 1,
                job      = xi.job.SMN,
                level    = level,
                sjob     = xi.job.WHM,
                slevel   = math.floor(level / 2),
                main     = weaponFor(xi.job.SMN, level),
                mob      = golden.mobIds[(n % #golden.mobIds) + 1],
                mlvl     = level + (li - 2) * 3,
                variant  = li == 2 and 1 or 0,
                pet      = bp.pet,
                bpSkills = bp.skills,
            })
        end
    end

    -- the merit char's avatar, with its avatar merits and job points
    table.insert(list,
    {
        name     = 'bp_c11_garuda',
        char     = 11,
        race     = golden.chars[11].race,
        job      = xi.job.SMN,
        level    = 99,
        sjob     = xi.job.WHM,
        slevel   = 49,
        main     = weaponFor(xi.job.SMN, 99),
        mob      = golden.mobIds[4],
        mlvl     = 99,
        variant  = 0,
        pet      = p.GARUDA,
        bpSkills = { 592 },
    })

    -- weapon skill scripts with the merits past their unlock (char 9) and without (a plain char); 95 is under the
    -- level the merits count at
    local wsmList =
    {
        { job = xi.job.THF, sjob = xi.job.NIN, main = 16480, ws = 'exenterator' },
        { job = xi.job.WAR, sjob = xi.job.NIN, main = 16643, ws = 'ruinator' },
        { job = xi.job.WAR, sjob = xi.job.NIN, main = 16535, ws = 'requiescat' },
    }

    for wi, w in ipairs(wsmList) do
        for li, level in ipairs({ 95, 99 }) do
            for ci, ch in ipairs({ 9, 5 }) do
                table.insert(list,
                {
                    name    = string.format('wsm_c%02d_j%02d_l%02d_%s', ch, w.job, level, w.ws),
                    char    = ch,
                    race    = golden.chars[ch].race,
                    job     = w.job,
                    level   = level,
                    sjob    = w.sjob,
                    slevel  = math.floor(level / 2),
                    main    = w.main,
                    mob     = golden.mobIds[((wi + li + ci) % #golden.mobIds) + 1],
                    mlvl    = 99,
                    variant = ci == 1 and 0 or 1,
                    wsm     = w.ws,
                })
            end
        end
    end

    -- Flash: its accuracy penalty by the time left (the effect started the given milliseconds ago)
    for ji, job in ipairs({ xi.job.WAR, xi.job.THF, xi.job.MNK }) do
        for li, level in ipairs({ 30, 75 }) do
            local n = ji + li
            table.insert(list,
            {
                name    = string.format('flash_j%02d_l%02d', job, level),
                race    = (n % 8) + 1,
                job     = job,
                level   = level,
                sjob    = job == xi.job.WAR and xi.job.NIN or xi.job.WAR,
                slevel  = math.floor(level / 2),
                main    = weaponFor(job, level),
                mob     = golden.mobIds[(n % #golden.mobIds) + 1],
                mlvl    = level + 2,
                variant = 0,
                flash   = true,
            })
        end
    end

    return list
end

-----------------------------------
-- Setup: the same binding calls on both servers
-----------------------------------
golden.findMob = function(id)
    local m = GetMobByID(id)
    if m and not m:isSpawned() then
        m = SpawnMob(id)
    end

    return m
end

-- the modifier deltas a case put on the player, taken off again before the next case (the job / level / equipment
-- bindings rebuild their own modifiers around them)
golden.applied = {}

local function addMods(entity, extra, sign)
    for name, v in pairs(extra) do
        entity:setMod(xi.mod[name], entity:getMod(xi.mod[name]) + sign * v)
    end
end

local fxEffects = { SIGNET = true, INNIN = true, YONIN = true }

golden.setupPlayer = function(p, c)
    if p:getTarget() then
        p:disengage()
    end

    for name in pairs(fxEffects) do
        p:delStatusEffectSilent(xi.effect[name])
    end

    for slot = 0, 15 do
        p:unequipItem(slot)
    end

    if golden.applied[p:getID()] then
        addMods(p, golden.applied[p:getID()], -1)
        golden.applied[p:getID()] = nil
    end

    p:changeJob(c.job)
    p:changesJob(c.sjob or 0)
    if c.sjob then
        p:setsLevel(c.slevel)
    end

    p:setLevel(c.level)
    p:capAllSkills()

    for _, item in ipairs({ c.main or 0, c.sub or 0, c.ranged or 0, c.ammo or 0 }) do
        if item ~= 0 and not p:hasItem(item) then
            p:addItem(item)
        end
    end

    if c.main then
        p:equipItem(c.main, nil, xi.slot.MAIN)
    end

    if c.sub then
        p:equipItem(c.sub, nil, xi.slot.SUB)
    end

    if c.ranged then
        p:equipItem(c.ranged, nil, xi.slot.RANGED)
    end

    if c.ammo then
        p:equipItem(c.ammo, nil, xi.slot.AMMO)
    end

    addMods(p, variants[c.variant].player, 1)
    golden.applied[p:getID()] = variants[c.variant].player
end

-- setMobLevel restores the mob's saved modifiers before CalculateMobStats, so the last case's deltas go with it
golden.setupMob = function(m, c)
    m:setMobLevel(c.mlvl)
    addMods(m, variants[c.variant].mob, 1)
end

-----------------------------------
-- The probe: emit(key, value) for every golden value of one case
-----------------------------------
local statMods = { 'STR', 'DEX', 'VIT', 'AGI', 'INT', 'MND', 'CHR' }

local function probeStats(prefix, e, emit)
    emit(prefix .. 'lvl', e:getMainLvl())
    for _, s in ipairs(statMods) do
        emit(prefix .. s, e:getStat(xi.mod[s]))
    end

    emit(prefix .. 'ATT', e:getStat(xi.mod.ATT))
    emit(prefix .. 'ATT.sub', e:getStat(xi.mod.ATT, xi.slot.SUB))
    emit(prefix .. 'ACC', e:getACC())
    emit(prefix .. 'ACC.1', e:getACC(1))
    emit(prefix .. 'ACC.2', e:getACC(2))
    emit(prefix .. 'DEF', e:getStat(xi.mod.DEF))
    emit(prefix .. 'EVA', e:getEVA())
    emit(prefix .. 'RATT', e:getRATT())
    emit(prefix .. 'RACC', e:getRACC())
    emit(prefix .. 'maxHP', e:getMaxHP())
    emit(prefix .. 'maxMP', e:getMaxMP())
    emit(prefix .. 'delay', e:getBaseDelay())
    emit(prefix .. 'wdmg', e:getWeaponDmg())
    emit(prefix .. 'wrank', e:getWeaponDmgRank())
end

golden.spells =
{
    { key = 'stone', id = 159, group = xi.magic.spellGroup.BLACK, skill = xi.skill.ELEMENTAL_MAGIC, stat = xi.mod.INT, element = xi.element.EARTH },
    { key = 'fire2', id = 145, group = xi.magic.spellGroup.BLACK, skill = xi.skill.ELEMENTAL_MAGIC, stat = xi.mod.INT, element = xi.element.FIRE },
    { key = 'banish', id = 28, group = xi.magic.spellGroup.WHITE, skill = xi.skill.DIVINE_MAGIC, stat = xi.mod.MND, element = xi.element.LIGHT },
    { key = 'slow', id = 56, group = xi.magic.spellGroup.WHITE, skill = xi.skill.ENFEEBLING_MAGIC, stat = xi.mod.MND, element = xi.element.EARTH, effect = xi.effect.SLOW },
}

-- the magic accuracy pieces and resist of every spell for every { key, actor, target, withBonusDiff } pair, the base
-- damage of the damaging ones
golden.probeMagic = function(pairs, spells, emit)
    local resist = xi.combat.magicHitRate.calculateResistRate
    local validate = upvalue(resist, 'validateParameters')
    local rank = upvalue(resist, 'calculateTargetResistanceRank')
    local macc = upvalue(resist, 'calculateActorMagicAccuracy')
    local meva = upvalue(resist, 'calculateTargetMagicEvasion')
    local mhr = upvalue(resist, 'calculateMagicHitRate')

    for _, s in ipairs(spells) do
        local fed = { magicalElement = s.element, actorStat = s.stat, skillType = s.skill, spellGroup = s.group, effectId = s.effect }
        for _, pair in ipairs(pairs) do
            local a, t = pair[2], pair[3]
            local params = validate(a, t, fed)
            params.resistanceRank = rank(a, t, params)
            emit(string.format('magic.%s.%s.rank', s.key, pair[1]), params.resistanceRank)
            params.actorMagicAccuracy = macc(a, t, params)
            emit(string.format('magic.%s.%s.macc', s.key, pair[1]), params.actorMagicAccuracy)
            params.targetMagicEvasion = meva(a, t, params)
            emit(string.format('magic.%s.%s.meva', s.key, pair[1]), params.targetMagicEvasion)
            emit(string.format('magic.%s.%s.hitrate', s.key, pair[1]), mhr(a, t, params))
            for _, mode in ipairs({ 'low', 'mid', 'high', 'mix3' }) do
                emit(string.format('magic.%s.%s.resist.%s', s.key, pair[1], mode), golden.withRng(golden.rngModes[mode], function()
                    return resist(a, t, fed)
                end))
            end
        end

        if s.group == xi.magic.spellGroup.BLACK or s.skill == xi.skill.DIVINE_MAGIC then
            for _, pair in ipairs(pairs) do
                emit(string.format('magic.%s.base.%s', s.key, pair[1]), xi.spells.damage.calculateBaseDamage(pair[2], pair[3], s.id, s.group, s.skill, s.stat))
            end

            for _, pair in ipairs(pairs) do
                if pair[4] then
                    emit(string.format('magic.%s.mbd.%s', s.key, pair[1]), xi.spells.damage.calculateMagicBonusDiff(pair[2], pair[3], s.id, s.skill, s.element, 0))
                end
            end
        end
    end
end

golden.probe = function(p, m, c, emit)
    local pid = p:getID()
    emit('equip.main', p:getEquipID(xi.slot.MAIN))
    emit('equip.sub', p:getEquipID(xi.slot.SUB))
    emit('equip.ranged', p:getEquipID(xi.slot.RANGED))
    emit('equip.ammo', p:getEquipID(xi.slot.AMMO))
    emit('p.subdmg', p:getOffhandDmg())
    emit('p.subrank', p:getOffhandDmgRank())
    emit('p.dw', p:getMod(xi.mod.DUAL_WIELD))
    emit('p.sjob', p:getSubJob())
    emit('p.slvl', p:getSubLvl())
    probeStats('p.', p, emit)
    probeStats('m.', m, emit)

    local wtype = p:getWeaponSkillType(xi.slot.MAIN)
    emit('p.wtype', wtype)
    emit('p.h2h', p:isUsingH2H())
    emit('p.2h', p:isWeaponTwoHanded())
    emit('p.skill.main', p:getSkillLevel(wtype))
    emit('p.skill.eva', p:getSkillLevel(xi.skill.EVASION))
    emit('p.skill.parry', p:getSkillLevel(xi.skill.PARRY))
    emit('p.skill.healing', p:getSkillLevel(xi.skill.HEALING_MAGIC))
    emit('p.skill.elemental', p:getSkillLevel(xi.skill.ELEMENTAL_MAGIC))
    emit('p.skill.enfeebling', p:getSkillLevel(xi.skill.ENFEEBLING_MAGIC))
    emit('p.maxskill.main', p:getMaxSkillLevel(c.level, c.job, wtype))
    emit('p.rdelay', p:getBaseRangedDelay())
    emit('p.rdmg', p:getRangedDmg())

    -- level correction as the formulas see it
    local lcp = xi.data.levelCorrection.isLevelCorrectedZone(p)
    local lcm = xi.data.levelCorrection.isLevelCorrectedZone(m)
    emit('lc.p', lcp)
    emit('lc.m', lcm)

    -- hit rate (battleutils::GetHitRate -> xi.combat.physicalHitRate.getPhysicalHitRate)
    local hr = xi.combat.physicalHitRate
    emit('hit.pm.0', hr.getPhysicalHitRate(p, m, 0, 0, false))
    emit('hit.pm.1', hr.getPhysicalHitRate(p, m, 0, 1, false))
    emit('hit.pm.0.acc20', hr.getPhysicalHitRate(p, m, 20, 0, false))
    emit('hit.mp.0', hr.getPhysicalHitRate(m, p, 0, 0, false))
    emit('hit.pm.ws', hr.getPhysicalHitRate(p, m, 0, 0, true))
    emit('hit.cap.p', hr.getPhysicalHitRateCap(p, 0))

    -- fSTR (battleutils::GetFSTR)
    local phys = xi.combat.physical
    emit('fstr.pm', phys.calculateMeleeStatFactor(p, m))
    emit('fstr.mp', phys.calculateMeleeStatFactor(m, p))
    emit('rfstr.pm', phys.calculateRangedStatFactor(p, m))

    -- pDIF (battleutils::GetDamageRatio -> calculateMeleePDIF) under every draw sequence
    for _, mode in ipairs(golden.rngModeOrder) do
        local f = golden.rngModes[mode]
        for _, crit in ipairs({ false, true }) do
            local ck = crit and 'crit' or 'norm'
            emit(string.format('pdif.pm.%s.%s', ck, mode), golden.withRng(f, function()
                return phys.calculateMeleePDIF(p, m, wtype, 1.0, crit, lcp, false, 0.0, false, xi.slot.MAIN, false)
            end))
            emit(string.format('pdif.mp.%s.%s', ck, mode), golden.withRng(f, function()
                return phys.calculateMeleePDIF(m, p, 0, 1.0, crit, lcm, false, 0.0, false, xi.slot.MAIN, false)
            end))
        end

        -- a weapon skill swing: attack bonus, defense ignored by TP
        emit('pdif.pm.ws.' .. mode, golden.withRng(f, function()
            return phys.calculateMeleePDIF(p, m, wtype, 1.25, false, lcp, true, 0.2, true, xi.slot.MAIN, false)
        end))
        emit('rpdif.pm.' .. mode, golden.withRng(f, function()
            return phys.calculateRangedPDIF(p, m, p:getWeaponSkillType(xi.slot.RANGED), 1.0, false, lcp, false, 0.0, false, 0)
        end))
    end

    -- TP (xi.combat.tp, called from battleutils)
    local tp = xi.combat.tp
    for _, delay in ipairs({ 96, 180, 240, 300, 360, 480, 500, 540, 600, 700, 800, 960 }) do
        emit('tp.ret.p.' .. delay, tp.calculateTPReturn(p, delay))
        emit('tp.ret.m.' .. delay, tp.calculateTPReturn(m, delay))
    end

    local pm = tp.getModifiedDelayAndCanZanshin(p, p:getBaseDelay())
    emit('tp.moddelay.p', pm.modifiedDelay)
    emit('tp.canzanshin.p', pm.canZanshin)
    emit('tp.hit.p', tp.getSingleMeleeHitTPReturn(p, false))
    emit('tp.hit.p.zanshin', tp.getSingleMeleeHitTPReturn(p, true))
    emit('tp.hit.m', tp.getSingleMeleeHitTPReturn(m, false))
    emit('tp.weapon.p.main', tp.getSingleWeaponTPReturn(p, xi.slot.MAIN))
    emit('tp.weapon.p.sub', tp.getSingleWeaponTPReturn(p, xi.slot.SUB))
    emit('tp.ranged.p', tp.getSingleRangedHitTPReturn(p))
    for _, dmg in ipairs({ 0, 1, 50, 400 }) do
        emit('tp.taken.m.' .. dmg, tp.calculateTPGainOnPhysicalDamage(p, m, dmg, p:getBaseDelay()))
        emit('tp.taken.p.' .. dmg, tp.calculateTPGainOnPhysicalDamage(m, p, dmg, m:getBaseDelay()))
    end

    -- critical hit rate for weapon skills, parry / guard / block rates
    emit('crit.ws.pm', phys.calculateSwingCriticalRate(p, m, 2000, xi.slot.MAIN, { 0.1, 0.25, 0.4 }))
    emit('crit.swing.pm', phys.calculateSwingCriticalRate(p, m, 0, xi.slot.MAIN, nil))
    emit('crit.swing.mp', phys.calculateSwingCriticalRate(m, p, 0, xi.slot.MAIN, nil))
    emit('crit.dex.pm', phys.criticalRateFromStatDiff(p, m))
    emit('parry.p', phys.calculateParryRate(p, m))
    emit('guard.p', phys.calculateGuardRate(p, m))
    emit('block.p', phys.calculateBlockRate(p, m))

    -- weapon skill pieces
    emit('ws.wsc.str', phys.calculateWSC(p, 0.4, 0, 0, 0, 0, 0, 0))
    emit('ws.wsc.mix', phys.calculateWSC(p, 0.2, 0.3, 0, 0, 0, 0.5, 0))
    emit('ws.alpha', phys.calculateAlpha(p))
    emit('ws.ftpbonus', phys.calculateFTPBonus(p))
    for _, t in ipairs({ 1000, 1500, 2000, 2999, 3000 }) do
        emit('ws.ftp.' .. t, xi.weaponskills.fTP(t, { 1.0, 1.5, 2.25 }))
        emit('ws.tpfactor.' .. t, phys.calculateTPfactor(t, { 0.1, 0.3, 0.5 }))
    end

    local meleeDmg = xi.weaponskills.getMeleeDmg(p, wtype, false)
    for i = 1, 2 do
        emit('ws.meleedmg.' .. i, meleeDmg[i])
    end

    emit('ws.hit', xi.weaponskills.getHitRate(p, m, 0, xi.slot.MAIN))

    -- magic: accuracy, evasion, hit rate and resist (xi.combat.magicHitRate), base damage (xi.spells.damage)
    golden.probeMagic({ { 'pm', p, m, true }, { 'mp', m, p } }, golden.spells, emit)

    -- cure: power and the final amount of Cure / Cure III's bands
    local power = getCurePower(p)
    emit('cure.power', power)
    emit('cure.power.old', getCurePowerOld(p))
    local cure = GetSpell(1)
    emit('cure.final.1', getCureFinal(p, cure, getBaseCure(power, 1.3333, 15, 20), 10, false))
    emit('cure.final.2', getCureFinal(p, cure, getBaseCure(power, 8.5, 30, 40), 10, false))
    emit('cure.final.min', getCureFinal(p, cure, 3, 10, false))

    -- day and weather (VanadielDayElement, getWeather): the capture runs on Firesday at noon without weather
    emit('day.element', VanadielDayElement())
    emit('weather.p', p:getWeather())
    for element = xi.element.FIRE, xi.element.DARK do
        emit('dayweather.' .. element, xi.spells.damage.calculateDayAndWeather(p, element, true))
    end

    -- damage taken (battleutils::PhysicalDmgTaken / RangedDmgTaken / CheckAndApplyDamageCap / HandleSevereDamage)
    for _, dmg in ipairs({ 1, 100, 999, 2500 }) do
        emit('taken.phys.p.' .. dmg, p:physicalDmgTaken(dmg, xi.damageType.SLASHING))
        emit('taken.phys.m.' .. dmg, m:physicalDmgTaken(dmg, xi.damageType.BLUNT))
        emit('taken.ranged.p.' .. dmg, p:rangedDmgTaken(dmg, xi.damageType.PIERCING))
        emit('taken.cap.p.' .. dmg, p:checkDamageCap(dmg))
        emit('taken.severe.p.' .. dmg, p:handleSevereDamage(dmg, true))
    end

    -- enmity from damage (CEnmityContainer::UpdateEnmityFromDamage)
    for _, dmg in ipairs({ 1, 100, 1500 }) do
        m:resetEnmity(p)
        m:updateEnmityFromDamage(p, dmg)
        emit('enmity.dmg.' .. dmg .. '.ce', m:getCE(p))
        emit('enmity.dmg.' .. dmg .. '.ve', m:getVE(p))
    end

    -- enmity from a cure on the char the mob hates (battleutils::GenerateCureEnmity, CEnmityContainer::UpdateEnmityFromCure)
    for _, amount in ipairs({ 1, 250, 2000 }) do
        m:resetEnmity(p)
        m:updateEnmityFromDamage(p, 10)
        p:updateEnmityFromCure(p, amount)
        emit('enmity.cure.' .. amount .. '.ce', m:getCE(p))
        emit('enmity.cure.' .. amount .. '.ve', m:getVE(p))
    end

    m:resetEnmity(p)
    emit('enmity.reset.ce', m:getCE(p))
    emit('enmity.reset.ve', m:getVE(p))

    -- a damaging spell's TP (battleutils::TakeSpellDamage: calculateSpellTP to the caster, calculateTPGainOnMagicalDamage
    -- to the target), 1 damage so nothing dies
    m:setHP(m:getMaxHP())
    m:setTP(0)
    p:setTP(0)
    m:takeSpellDamage(p, GetSpell(159), 1, xi.attackType.MAGICAL, xi.damageType.EARTH)
    emit('spell.taken.m.hp', m:getMaxHP() - m:getHP())
    emit('spell.taken.m.tp', m:getTP())
    emit('spell.taken.p.tp', p:getTP())
    m:setHP(m:getMaxHP())
    m:setTP(0)
    p:setTP(0)
    emit('pid.ok', pid == p:getID())
end

-- the effects cases: the geometry and state the modifiers read, then the stats, hit rates, pDIF and crit rates
golden.probeFx = function(p, m, c, emit)
    local target = p:getTarget()
    emit('fx.target', target and target:getID() == m:getID() or false)
    emit('fx.behind.pm', p:isBehind(m, 23))
    emit('fx.facing.pm', p:isFacing(m))
    emit('fx.facing.pm.64', p:isFacing(m, 64))
    emit('fx.facing.mp', m:isFacing(p))
    emit('fx.difficulty', p:checkDifficulty(m))
    emit('fx.region', p:getCurrentRegion())
    emit('fx.effect', p:hasStatusEffect(xi.effect[string.upper(c.fx)]))
    probeStats('p.', p, emit)
    probeStats('m.', m, emit)

    local wtype = p:getWeaponSkillType(xi.slot.MAIN)
    emit('p.wtype', wtype)
    local lcp = xi.data.levelCorrection.isLevelCorrectedZone(p)
    local lcm = xi.data.levelCorrection.isLevelCorrectedZone(m)
    emit('lc.p', lcp)
    local hr = xi.combat.physicalHitRate
    emit('hit.pm.0', hr.getPhysicalHitRate(p, m, 0, 0, false))
    emit('hit.pm.1', hr.getPhysicalHitRate(p, m, 0, 1, false))
    emit('hit.pm.0.acc20', hr.getPhysicalHitRate(p, m, 20, 0, false))
    emit('hit.mp.0', hr.getPhysicalHitRate(m, p, 0, 0, false))
    emit('hit.pm.ws', hr.getPhysicalHitRate(p, m, 0, 0, true))

    local phys = xi.combat.physical
    emit('fstr.pm', phys.calculateMeleeStatFactor(p, m))
    emit('fstr.mp', phys.calculateMeleeStatFactor(m, p))
    for _, mode in ipairs(golden.rngModeOrder) do
        local f = golden.rngModes[mode]
        for _, crit in ipairs({ false, true }) do
            local ck = crit and 'crit' or 'norm'
            emit(string.format('pdif.pm.%s.%s', ck, mode), golden.withRng(f, function()
                return phys.calculateMeleePDIF(p, m, wtype, 1.0, crit, lcp, false, 0.0, false, xi.slot.MAIN, false)
            end))
            emit(string.format('pdif.mp.%s.%s', ck, mode), golden.withRng(f, function()
                return phys.calculateMeleePDIF(m, p, 0, 1.0, crit, lcm, false, 0.0, false, xi.slot.MAIN, false)
            end))
        end
    end

    emit('crit.swing.pm', phys.calculateSwingCriticalRate(p, m, 0, xi.slot.MAIN, nil))
    emit('crit.swing.mp', phys.calculateSwingCriticalRate(m, p, 0, xi.slot.MAIN, nil))
    emit('tp.hit.p', xi.combat.tp.getSingleMeleeHitTPReturn(p, false))
    emit('tp.taken.m.50', xi.combat.tp.calculateTPGainOnPhysicalDamage(p, m, 50, p:getBaseDelay()))
end

-- the pet cases: the pet's stats and skills, then the formulas with the pet as attacker and as target
golden.probePet = function(p, pet, m, c, emit)
    emit('pet.id', pet:getPetID())
    emit('pet.isPet', pet:isPet())
    emit('pet.isAvatar', pet:isAvatar())
    emit('pet.isAutomaton', pet:isAutomaton())
    emit('pet.isJugPet', pet:isJugPet())
    emit('pet.master', pet:getMaster() ~= nil and pet:getMaster():getID() == p:getID())
    emit('pet.element', p:getPetElement())
    probeStats('pet.', pet, emit)
    emit('pet.HP', pet:getHP())
    emit('pet.MP', pet:getMP())
    emit('pet.TP', pet:getTP())
    probeStats('m.', m, emit)
    emit('p.lvl', p:getMainLvl())
    emit('p.skill.summoning', p:getSkillLevel(xi.skill.SUMMONING_MAGIC))

    local wtype = pet:getWeaponSkillType(xi.slot.MAIN)
    emit('pet.wtype', wtype)
    for _, skill in ipairs({ xi.skill.HAND_TO_HAND, xi.skill.EVASION, xi.skill.PARRY, xi.skill.ELEMENTAL_MAGIC, xi.skill.ENFEEBLING_MAGIC, xi.skill.AUTOMATON_MELEE }) do
        emit('pet.skill.' .. skill, pet:getSkillLevel(skill))
    end

    local lcpet = xi.data.levelCorrection.isLevelCorrectedZone(pet)
    local lcm = xi.data.levelCorrection.isLevelCorrectedZone(m)
    emit('lc.pet', lcpet)

    local hr = xi.combat.physicalHitRate
    emit('hit.petm.0', hr.getPhysicalHitRate(pet, m, 0, 0, false))
    emit('hit.petm.1', hr.getPhysicalHitRate(pet, m, 0, 1, false))
    emit('hit.petm.0.acc20', hr.getPhysicalHitRate(pet, m, 20, 0, false))
    emit('hit.petm.ws', hr.getPhysicalHitRate(pet, m, 0, 0, true))
    emit('hit.mpet.0', hr.getPhysicalHitRate(m, pet, 0, 0, false))
    emit('hit.cap.pet', hr.getPhysicalHitRateCap(pet, 0))
    emit('rhit.petm', hr.getRangedHitRate(pet, m, 0, false))

    local phys = xi.combat.physical
    emit('fstr.petm', phys.calculateMeleeStatFactor(pet, m))
    emit('fstr.mpet', phys.calculateMeleeStatFactor(m, pet))
    emit('rfstr.petm', phys.calculateRangedStatFactor(pet, m))
    for _, mode in ipairs(golden.rngModeOrder) do
        local f = golden.rngModes[mode]
        for _, crit in ipairs({ false, true }) do
            local ck = crit and 'crit' or 'norm'
            emit(string.format('pdif.petm.%s.%s', ck, mode), golden.withRng(f, function()
                return phys.calculateMeleePDIF(pet, m, wtype, 1.0, crit, lcpet, false, 0.0, false, xi.slot.MAIN, false)
            end))
            emit(string.format('pdif.mpet.%s.%s', ck, mode), golden.withRng(f, function()
                return phys.calculateMeleePDIF(m, pet, 0, 1.0, crit, lcm, false, 0.0, false, xi.slot.MAIN, false)
            end))
        end
    end

    emit('crit.swing.petm', phys.calculateSwingCriticalRate(pet, m, 0, xi.slot.MAIN, nil))
    emit('crit.swing.mpet', phys.calculateSwingCriticalRate(m, pet, 0, xi.slot.MAIN, nil))
    emit('crit.dex.petm', phys.criticalRateFromStatDiff(pet, m))

    local tp = xi.combat.tp
    for _, delay in ipairs({ 180, 240, 480, 600 }) do
        emit('tp.ret.pet.' .. delay, tp.calculateTPReturn(pet, delay))
    end

    emit('tp.hit.pet', tp.getSingleMeleeHitTPReturn(pet, false))
    for _, dmg in ipairs({ 0, 1, 50, 400 }) do
        emit('tp.taken.bypet.' .. dmg, tp.calculateTPGainOnPhysicalDamage(pet, m, dmg, pet:getBaseDelay()))
        emit('tp.taken.pet.' .. dmg, tp.calculateTPGainOnPhysicalDamage(m, pet, dmg, m:getBaseDelay()))
    end

    golden.probeMagic({ { 'petm', pet, m, true }, { 'mpet', m, pet } }, golden.spells, emit)
    if pet:isAvatar() then
        emit('pet.skillovercap', xi.summon.getSummoningSkillOverCap(pet))
        emit('pet.maccbonus', xi.mobskills.calculatePetMagicAccuracyBonus(pet, m, xi.element.FIRE))
    end

    for _, dmg in ipairs({ 1, 100, 999 }) do
        emit('taken.phys.pet.' .. dmg, pet:physicalDmgTaken(dmg, xi.damageType.SLASHING))
        emit('taken.ranged.pet.' .. dmg, pet:rangedDmgTaken(dmg, xi.damageType.PIERCING))
    end

    -- enmity from the pet's damage, and from its cure on the master (the master is the curer)
    for _, dmg in ipairs({ 1, 100, 1500 }) do
        m:resetEnmity(pet)
        m:updateEnmityFromDamage(pet, dmg)
        emit('enmity.petdmg.' .. dmg .. '.ce', m:getCE(pet))
        emit('enmity.petdmg.' .. dmg .. '.ve', m:getVE(pet))
    end

    m:resetEnmity(pet)
    m:resetEnmity(p)
    m:updateEnmityFromDamage(p, 10)
    pet:updateEnmityFromCure(p, 300)
    emit('enmity.petcure.ce', m:getCE(p))
    emit('enmity.petcure.ve', m:getVE(p))
    m:resetEnmity(p)

    -- a damaging spell on the pet: the TP it gains
    pet:setTP(0)
    m:setTP(0)
    local hp = pet:getHP()
    pet:takeSpellDamage(m, GetSpell(159), 1, xi.attackType.MAGICAL, xi.damageType.EARTH)
    emit('spell.taken.pet.hp', hp - pet:getHP())
    emit('spell.taken.pet.tp', pet:getTP())
    emit('spell.taken.m.tp', m:getTP())
    pet:setHP(hp)
    pet:setTP(0)
    m:setTP(0)

    -- a weapon skill (a blood pact's damage path) by the pet and on the pet: the TP both gain
    m:setHP(m:getMaxHP())
    m:resetEnmity(pet)
    emit('ws.petm', m:takeWeaponskillDamage(pet, 200, xi.attackType.PHYSICAL, xi.damageType.BLUNT, xi.slot.MAIN, true, 1.0, 0, 1.0))
    emit('ws.petm.pet.tp', pet:getTP())
    emit('ws.petm.m.tp', m:getTP())
    emit('ws.petm.ce', m:getCE(pet))
    emit('ws.petm.ve', m:getVE(pet))
    pet:setTP(0)
    m:setTP(0)
    emit('ws.mpet', pet:takeWeaponskillDamage(m, 1, xi.attackType.PHYSICAL, xi.damageType.BLUNT, xi.slot.MAIN, true, 1.0, 0, 1.0))
    emit('ws.mpet.pet.tp', pet:getTP())
    emit('ws.mpet.m.tp', m:getTP())
    pet:setHP(hp)
    pet:setTP(0)
    m:setTP(0)
    m:setHP(m:getMaxHP())
    m:resetEnmity(pet)
end

-- the merit / job point cases: the merits and job points the formulas read, then the full probe and the hit rates
-- with the geometry the merits look at
local probedMerits = { 'MAX_HP', 'MAX_MP', 'STR', 'DEX', 'AGI', 'INT', 'MND', 'CHR', 'DAGGER_SKILL', 'KATANA_SKILL', 'EVASION_SKILL',
    'CRIT_HIT_RATE', 'ENEMY_CRIT_RATE', 'DOUBLE_ATTACK_RATE', 'TRIPLE_ATTACK_RATE', 'STORE_TP_EFFECT', 'SUBTLE_BLOW_EFFECT',
    'KICK_ATTACK_RATE', 'ZANSHIN_ATTACK_RATE', 'AMBUSH', 'INNIN_EFFECT', 'YONIN_EFFECT', 'IKISHOTEN', 'CLOSED_POSITION',
    'NIN_MAGIC_ACCURACY', 'ELEMENTAL_MAGIC_SKILL', 'FIRE_MAGIC_POTENCY', 'EARTH_MAGIC_ACCURACY', 'BANISH_EFFECT',
    'ELEMENTAL_MAGIC_ACCURACY', 'ANCIENT_MAGIC_ATK_BONUS', 'MAGIC_ACCURACY', 'ACCURACY', 'SUMMONING_MAGIC_SKILL',
    'AVATAR_PHYSICAL_ACCURACY', 'AVATAR_PHYSICAL_ATTACK', 'AUTOMATON_SKILLS', 'BEAST_AFFINITY' }
local probedJobPoints = { 'INNIN_EFFECT', 'YONIN_EFFECT', 'NINJITSU_ACC_BONUS', 'MIGHTY_STRIKES_EFFECT', 'BERSERK_EFFECT',
    'RESTRAINT_EFFECT', 'SNEAK_ATTACK_EFFECT', 'WHM_MAGIC_ACC_BONUS', 'BLM_MAGIC_ACC_BONUS', 'MAGIC_DMG_BONUS',
    'RDM_MAGIC_ACC_BONUS', 'SUMMON_ACC_BONUS', 'SUMMON_PHYS_ATK_BONUS', 'WYVERN_MAX_HP_BONUS' }

golden.probeMerit = function(p, m, c, emit)
    for _, name in ipairs(probedMerits) do
        emit('merit.' .. name, p:getMerit(xi.merit[name]))
    end

    for _, name in ipairs(probedJobPoints) do
        emit('jp.' .. name, p:getJobPointLevel(xi.jp[name]))
    end

    emit('jp.spent', p:getSpentJobPoints())
    emit('fx.behind.pm', p:isBehind(m, 23))
    emit('fx.facing.pm', p:isFacing(m))
    emit('fx.facing.mp', m:isFacing(p))
    emit('fx.trait.ambush', p:hasTrait(xi.trait.AMBUSH))
    for _, skill in ipairs({ xi.skill.DAGGER, xi.skill.KATANA, xi.skill.SUMMONING_MAGIC, xi.skill.DIVINE_MAGIC, xi.skill.NINJUTSU }) do
        emit('p.skill.' .. skill, p:getSkillLevel(skill))
    end

    golden.probe(p, m, c, emit)

    local hr = xi.combat.physicalHitRate
    local acc, eva = hr.getHitRateModifiers(p, m, false, false)
    emit('hitmods.pm.acc', acc)
    emit('hitmods.pm.eva', eva)
    acc, eva = hr.getHitRateModifiers(m, p, false, false)
    emit('hitmods.mp.acc', acc)
    emit('hitmods.mp.eva', eva)
    emit('rhit.pm', hr.getRangedHitRate(p, m, 0, false))
    emit('crit.ranged.pm', xi.combat.physical.calculateRangedCriticalRate(p, m, 0, xi.slot.RANGED, nil))
    emit('crit.ranged.mp', xi.combat.physical.calculateRangedCriticalRate(m, p, 0, xi.slot.RANGED, nil))
    emit('crit.ws.pm.merit', xi.combat.physical.calculateSwingCriticalRate(p, m, 1500, xi.slot.MAIN, { 0.1, 0.2, 0.3 }))
    local multi = upvalue(xi.weaponskills.calculateRawWSDmg, 'getMultiAttacks')
    for _, f in ipairs({ 0, 0.03, 0.05, 0.5 }) do
        emit('ws.multihit.' .. f, golden.withRng({ f }, function()
            return multi(p, m, {}, true, false)
        end))
    end
end

-- cure enmity through another char: the mob hates the cured char (or only the healer), the healer cures it
golden.probeCure = function(p, o, m, c, emit)
    emit('cure.target.lvl', o:getMainLvl())
    emit('cure.healer.lvl', p:getMainLvl())
    for _, amount in ipairs({ 1, 250, 2000 }) do
        -- the mob hates the target only
        m:resetEnmity(p)
        m:resetEnmity(o)
        m:updateEnmityFromDamage(o, 10)
        p:updateEnmityFromCure(o, amount)
        emit('enmity.cure.' .. amount .. '.healer.ce', m:getCE(p))
        emit('enmity.cure.' .. amount .. '.healer.ve', m:getVE(p))
        emit('enmity.cure.' .. amount .. '.target.ce', m:getCE(o))
        emit('enmity.cure.' .. amount .. '.target.ve', m:getVE(o))

        -- the mob hates the healer and the target
        m:updateEnmityFromDamage(p, 200)
        p:updateEnmityFromCure(o, amount)
        emit('enmity.cure.' .. amount .. '.both.ce', m:getCE(p))
        emit('enmity.cure.' .. amount .. '.both.ve', m:getVE(p))

        -- fixed enmity
        p:updateEnmityFromCure(o, amount, 40, 120)
        emit('enmity.cure.' .. amount .. '.fixed.ce', m:getCE(p))
        emit('enmity.cure.' .. amount .. '.fixed.ve', m:getVE(p))

        -- the mob hates the healer only: the cured char is not on its list
        m:resetEnmity(p)
        m:resetEnmity(o)
        m:updateEnmityFromDamage(p, 10)
        p:updateEnmityFromCure(o, amount)
        emit('enmity.cure.' .. amount .. '.other.ce', m:getCE(p))
        emit('enmity.cure.' .. amount .. '.other.ve', m:getVE(p))
    end

    m:resetEnmity(p)
    m:resetEnmity(o)
end

-- the job ability cases: each ability's own Lua (xi.job_utils), then the effect it left and what it changes in the
-- stats and formulas; the effect is taken off again after
local fakeAbility = setmetatable({}, { __index = function()
    return function()
        return 0
    end
end })

local abilityMods = { 'ATTP', 'DEFP', 'ACC', 'EVA', 'HASTE_ABILITY', 'TWOHAND_HASTE_ABILITY', 'TWOHAND_STR', 'TWOHAND_ACC', 'CRITHITRATE',
    'KICK_ATTACK_RATE', 'KICK_DMG', 'COUNTER', 'DOUBLE_ATTACK', 'TRIPLE_ATTACK', 'ENMITY', 'UTSUSEMI', 'SUBTLE_BLOW', 'STORETP',
    'DMGPHYS', 'ZANSHIN', 'HASSO_ZANSHIN_BONUS', 'SEIGAN_COUNTER_BONUS', 'RATTP', 'MATT' }

golden.abilities =
{
    berserk      = { xi.job_utils.warrior, 'useBerserk', xi.effect.BERSERK },
    warcry       = { xi.job_utils.warrior, 'useWarcry', xi.effect.WARCRY },
    aggressor    = { xi.job_utils.warrior, 'useAggressor', xi.effect.AGGRESSOR },
    defender     = { xi.job_utils.warrior, 'useDefender', xi.effect.DEFENDER },
    mighty       = { xi.job_utils.warrior, 'useMightyStrikes', xi.effect.MIGHTY_STRIKES },
    restraint    = { xi.job_utils.warrior, 'useRestraint', xi.effect.RESTRAINT },
    footwork     = { xi.job_utils.monk, 'useFootwork', xi.effect.FOOTWORK },
    counterstance = { xi.job_utils.monk, 'useCounterstance', xi.effect.COUNTERSTANCE },
    focus        = { xi.job_utils.monk, 'useFocus', xi.effect.FOCUS },
    dodge        = { xi.job_utils.monk, 'useDodge', xi.effect.DODGE },
    hasso        = { xi.job_utils.samurai, 'useHasso', xi.effect.HASSO },
    seigan       = { xi.job_utils.samurai, 'useSeigan', xi.effect.SEIGAN },
    thirdeye     = { xi.job_utils.samurai, 'useThirdEye', xi.effect.THIRD_EYE },
    meikyo       = { xi.job_utils.samurai, 'useMeikyoShisui', xi.effect.MEIKYO_SHISUI },
    yonin        = { xi.job_utils.ninja, 'useYonin', xi.effect.YONIN },
    innin        = { xi.job_utils.ninja, 'useInnin', xi.effect.INNIN },
    lastresort   = { xi.job_utils.dark_knight, 'useLastResort', xi.effect.LAST_RESORT },
    arcanecircle = { xi.job_utils.dark_knight, 'useArcaneCircle', xi.effect.ARCANE_CIRCLE },
    utsusemi1    = { 'spell', xi.magic.spell.UTSUSEMI_ICHI, xi.effect.COPY_IMAGE },
    utsusemi2    = { 'spell', xi.magic.spell.UTSUSEMI_NI, xi.effect.COPY_IMAGE },
}

golden.probeAbility = function(p, m, c, emit)
    local hr = xi.combat.physicalHitRate
    local phys = xi.combat.physical
    local wtype = p:getWeaponSkillType(xi.slot.MAIN)
    emit('p.wtype', wtype)
    emit('p.2h', p:isWeaponTwoHanded())

    local function snapshot(prefix)
        probeStats(prefix .. 'p.', p, emit)
        for _, name in ipairs(abilityMods) do
            emit(prefix .. 'mod.' .. name, p:getMod(xi.mod[name]))
        end

        local lcp = xi.data.levelCorrection.isLevelCorrectedZone(p)
        local lcm = xi.data.levelCorrection.isLevelCorrectedZone(m)
        emit(prefix .. 'hit.pm.0', hr.getPhysicalHitRate(p, m, 0, 0, false))
        emit(prefix .. 'hit.pm.2', hr.getPhysicalHitRate(p, m, 0, 2, false))
        emit(prefix .. 'hit.mp.0', hr.getPhysicalHitRate(m, p, 0, 0, false))
        emit(prefix .. 'rhit.pm', hr.getRangedHitRate(p, m, 0, false))
        for _, mode in ipairs({ 'mid', 'mix3' }) do
            local f = golden.rngModes[mode]
            emit(prefix .. 'pdif.pm.norm.' .. mode, golden.withRng(f, function()
                return phys.calculateMeleePDIF(p, m, wtype, 1.0, false, lcp, false, 0.0, false, xi.slot.MAIN, false)
            end))
            emit(prefix .. 'pdif.pm.crit.' .. mode, golden.withRng(f, function()
                return phys.calculateMeleePDIF(p, m, wtype, 1.0, true, lcp, false, 0.0, false, xi.slot.MAIN, false)
            end))
            emit(prefix .. 'pdif.mp.norm.' .. mode, golden.withRng(f, function()
                return phys.calculateMeleePDIF(m, p, 0, 1.0, false, lcm, false, 0.0, false, xi.slot.MAIN, false)
            end))
        end

        emit(prefix .. 'fstr.pm', phys.calculateMeleeStatFactor(p, m))
        emit(prefix .. 'crit.swing.pm', phys.calculateSwingCriticalRate(p, m, 0, xi.slot.MAIN, nil))
        emit(prefix .. 'crit.swing.mp', phys.calculateSwingCriticalRate(m, p, 0, xi.slot.MAIN, nil))
        emit(prefix .. 'crit.ranged.pm', phys.calculateRangedCriticalRate(p, m, 0, xi.slot.RANGED, nil))
        emit(prefix .. 'parry.p', phys.calculateParryRate(p, m))
        emit(prefix .. 'guard.p', phys.calculateGuardRate(p, m))
        emit(prefix .. 'tp.hit.p', xi.combat.tp.getSingleMeleeHitTPReturn(p, false))
        emit(prefix .. 'tp.hit.p.zanshin', xi.combat.tp.getSingleMeleeHitTPReturn(p, true))
        emit(prefix .. 'tp.taken.m.50', xi.combat.tp.calculateTPGainOnPhysicalDamage(p, m, 50, p:getBaseDelay()))
        emit(prefix .. 'tp.taken.p.50', xi.combat.tp.calculateTPGainOnPhysicalDamage(m, p, 50, m:getBaseDelay()))
        emit(prefix .. 'taken.phys.p.500', p:physicalDmgTaken(500, xi.damageType.SLASHING))
        m:resetEnmity(p)
        m:updateEnmityFromDamage(p, 100)
        emit(prefix .. 'enmity.dmg.100.ce', m:getCE(p))
        emit(prefix .. 'enmity.dmg.100.ve', m:getVE(p))
        m:resetEnmity(p)
    end

    snapshot('base.')
    for _, name in ipairs(c.abilities) do
        local a = golden.abilities[name]
        local prefix = 'ja.' .. name .. '.'
        p:setTP(0)
        if a[1] == 'spell' then
            emit(prefix .. 'ret', xi.spells.enhancing.useEnhancingNinjutsu(p, p, GetSpell(a[2])))
        else
            emit(prefix .. 'ret', a[1][a[2]](p, p, fakeAbility))
        end

        local effect = p:getStatusEffect(a[3])
        emit(prefix .. 'eff', effect ~= nil)
        if effect then
            emit(prefix .. 'power', effect:getPower())
            emit(prefix .. 'subPower', effect:getSubPower())
            emit(prefix .. 'duration', effect:getDuration())
            emit(prefix .. 'tier', effect:getTier())
        end

        emit(prefix .. 'tp', p:getTP())
        snapshot(prefix)
        p:delStatusEffectSilent(a[3])
        p:setTP(0)
    end
end

-- the spell cases: the spells' own Lua (damage, enfeebling, songs, enhancing) under each draw sequence, with the
-- damage, effect, TP and enmity they leave; the effects are taken off again after
golden.spellKinds =
{
    stone      = { 'dmg', 'STONE' },
    fire2      = { 'dmg', 'FIRE_II' },
    stonega    = { 'dmg', 'STONEGA' },
    banish     = { 'dmg', 'BANISH' },
    geohelix   = { 'helix', 'GEOHELIX' },
    luminohelix = { 'helix', 'LUMINOHELIX' },
    katon      = { 'dmg', 'KATON_ICHI' },
    silence    = { 'enf', 'SILENCE', xi.effect.SILENCE },
    sleep      = { 'enf', 'SLEEP', xi.effect.SLEEP_I },
    paralyze   = { 'enf', 'PARALYZE', xi.effect.PARALYSIS },
    slow       = { 'enf', 'SLOW', xi.effect.SLOW },
    slow2      = { 'enf', 'SLOW_II', xi.effect.SLOW },
    poison     = { 'enf', 'POISON', xi.effect.POISON },
    blind      = { 'enf', 'BLIND', xi.effect.BLINDNESS },
    gravity    = { 'enf', 'GRAVITY', xi.effect.WEIGHT },
    bind       = { 'enf', 'BIND', xi.effect.BIND },
    burn       = { 'enf', 'BURN', xi.effect.BURN },
    distract   = { 'enf', 'DISTRACT', xi.effect.EVASION_DOWN },
    hojo       = { 'enf', 'HOJO_ICHI', xi.effect.SLOW },
    kurayami   = { 'enf', 'KURAYAMI_ICHI', xi.effect.BLINDNESS },
    requiem    = { 'enfsong', 'FOE_REQUIEM', xi.effect.REQUIEM },
    lullaby    = { 'enfsong', 'FOE_LULLABY', xi.effect.LULLABY },
    minuet     = { 'song', 'VALOR_MINUET', xi.effect.MINUET },
    minuet4    = { 'song', 'VALOR_MINUET_IV', xi.effect.MINUET },
    madrigal   = { 'song', 'SWORD_MADRIGAL', xi.effect.MADRIGAL },
    paeon      = { 'song', 'ARMYS_PAEON', xi.effect.PAEON },
    minne      = { 'song', 'KNIGHTS_MINNE', xi.effect.MINNE },
    march      = { 'song', 'ADVANCING_MARCH', xi.effect.MARCH },
    protect    = { 'enh', 'PROTECT', xi.effect.PROTECT },
    shell      = { 'enh', 'SHELL', xi.effect.SHELL },
    barfire    = { 'enh', 'BARFIRE', xi.effect.BARFIRE },
    phalanx    = { 'enh', 'PHALANX', xi.effect.PHALANX },
    haste      = { 'enh', 'HASTE', xi.effect.HASTE },
    regen      = { 'enh', 'REGEN', xi.effect.REGEN },
    refresh    = { 'enh', 'REFRESH', xi.effect.REFRESH },
    stoneskin  = { 'enh', 'STONESKIN', xi.effect.STONESKIN },
    blink      = { 'enh', 'BLINK', xi.effect.BLINK },
    enfire     = { 'enh', 'ENFIRE', xi.effect.ENFIRE },
}

golden.probeSpell = function(p, m, c, emit)
    emit('p.skill.elemental', p:getSkillLevel(xi.skill.ELEMENTAL_MAGIC))
    emit('p.skill.enfeebling', p:getSkillLevel(xi.skill.ENFEEBLING_MAGIC))
    emit('p.skill.enhancing', p:getSkillLevel(xi.skill.ENHANCING_MAGIC))
    emit('p.skill.singing', p:getSkillLevel(xi.skill.SINGING))
    emit('p.skill.ninjutsu', p:getSkillLevel(xi.skill.NINJUTSU))
    emit('p.INT', p:getStat(xi.mod.INT))
    emit('p.MND', p:getStat(xi.mod.MND))
    emit('p.CHR', p:getStat(xi.mod.CHR))
    emit('m.lvl', m:getMainLvl())

    local function effectState(prefix, target, effectId)
        local effect = target:getStatusEffect(effectId)
        emit(prefix .. 'eff', effect ~= nil)
        if effect then
            emit(prefix .. 'power', effect:getPower())
            emit(prefix .. 'subPower', effect:getSubPower())
            emit(prefix .. 'duration', effect:getDuration())
            emit(prefix .. 'tier', effect:getTier())
        end
    end

    for _, key in ipairs(c.spells) do
        local kind = golden.spellKinds[key]
        local spell = GetSpell(xi.magic.spell[kind[2]])
        local base = 'sp.' .. key .. '.'
        for _, mode in ipairs({ 'low', 'mid', 'high', 'mix3' }) do
            local prefix = base .. mode .. '.'
            local f = golden.rngModes[mode]
            m:setHP(m:getMaxHP())
            m:setTP(0)
            p:setTP(0)
            m:resetEnmity(p)
            if kind[1] == 'dmg' or kind[1] == 'helix' then
                local dmg = golden.withRng(f, function()
                    return xi.spells.damage.useDamageSpell(p, m, spell)
                end)
                emit(prefix .. 'dmg', dmg)
                emit(prefix .. 'hp', m:getMaxHP() - m:getHP())
                emit(prefix .. 'm.tp', m:getTP())
                emit(prefix .. 'ce', m:getCE(p))
                emit(prefix .. 've', m:getVE(p))
                if kind[1] == 'helix' then
                    emit(prefix .. 'duration', xi.spells.enfeebling.calculateDuration(p, m, spell:getID(), xi.effect.HELIX, xi.skill.ELEMENTAL_MAGIC))
                end
            elseif kind[1] == 'enf' or kind[1] == 'enfsong' then
                m:delStatusEffectSilent(kind[3])
                local ret = golden.withRng(f, function()
                    if kind[1] == 'enf' then
                        return xi.spells.enfeebling.useEnfeeblingSpell(p, m, spell)
                    end

                    return xi.spells.enfeebling.useEnfeeblingSong(p, m, spell)
                end)
                emit(prefix .. 'ret', ret)
                effectState(prefix, m, kind[3])
                m:delStatusEffectSilent(kind[3])
            else
                p:delStatusEffectSilent(kind[3])
                local ret = golden.withRng(f, function()
                    if kind[1] == 'song' then
                        return xi.spells.enhancing.useEnhancingSong(p, p, spell)
                    end

                    return xi.spells.enhancing.useEnhancingSpell(p, p, spell)
                end)
                emit(prefix .. 'ret', ret)
                effectState(prefix, p, kind[3])
                p:delStatusEffectSilent(kind[3])
            end
        end

        -- the damage pieces of a damaging spell
        if kind[1] == 'dmg' or kind[1] == 'helix' then
            local d = xi.spells.damage
            local id, element, skill, group = spell:getID(), spell:getElement(), spell:getSkillType(), spell:getSpellGroup()
            local stat = skill == xi.skill.DIVINE_MAGIC and xi.mod.MND or xi.mod.INT
            emit(base .. 'base', d.calculateBaseDamage(p, m, id, group, skill, stat))
            emit(base .. 'mtdr', d.calculateMTDR(p, spell))
            emit(base .. 'staff', d.calculateElementalStaffBonus(p, element))
            emit(base .. 'affinity', d.calculateElementalAffinityBonus(p, element))
            emit(base .. 'addresist', d.calculateAdditionalResistTier(p, m, element))
            emit(base .. 'dayweather', d.calculateDayAndWeather(p, element, false))
            emit(base .. 'mbd', d.calculateMagicBonusDiff(p, m, id, skill, element, 0))
            emit(base .. 'adjust', xi.combat.damage.calculateDamageAdjustment(m, false, true, false, false))
            emit(base .. 'sdt', xi.combat.damage.magicalElementSDT(m, element))
            emit(base .. 'eco', xi.combat.damage.ecosystemMultiplier(p, m, 0))
            emit(base .. 'nin', d.calculateNinjutsuMultiplier(p, m, skill))
            emit(base .. 'ninskill', d.calculateNinSkillBonus(p, id, skill))
            emit(base .. 'undead', d.calculateUndeadDivinePenalty(m, skill))
            emit(base .. 'helix', d.calculateHelixMeritMultiplier(p, id))
            emit(base .. 'aoe', d.calculateAreaOfEffectResistance(m, spell))
            emit(base .. 'absorb', d.calculateAbsorption(m, element, false, true, false, false))
            emit(base .. 'null', d.calculateNullification(m, element, false, true, false, false))
            emit(base .. 'mburst', xi.combat.magicBurst.getMagicBurstTier(m, element))
        end

        -- the potency / duration pieces of an enfeebling spell
        if kind[1] == 'enf' then
            local skill = spell:getSkillType()
            emit(base .. 'potency', xi.spells.enfeebling.calculatePotency(p, m, spell:getID(), kind[3], skill, skill == xi.skill.ENFEEBLING_MAGIC and xi.mod.MND or xi.mod.INT))
            emit(base .. 'duration', xi.spells.enfeebling.calculateDuration(p, m, spell:getID(), kind[3], skill))
        end
    end

    m:setHP(m:getMaxHP())
    m:setTP(0)
    p:setTP(0)
    m:resetEnmity(p)
end

-- the weapon skill damage cases: battleutils::TakeWeaponskillDamage through takeWeaponskillDamage, the attacker's TP
-- from the weapon skill (CalculateTPFromDamageDealt), the target's (CalculateTPFromDamageTaken), the damage and enmity
local wsConfigs =
{
    { key = 'main', slot = xi.slot.MAIN, atk = xi.attackType.PHYSICAL, dmg = xi.damageType.SLASHING, primary = true, tpm = 1.0, bonus = 0, ttpm = 1.0 },
    { key = 'mainx', slot = xi.slot.MAIN, atk = xi.attackType.PHYSICAL, dmg = xi.damageType.SLASHING, primary = true, tpm = 1.5, bonus = 100, ttpm = 0.5 },
    { key = 'second', slot = xi.slot.MAIN, atk = xi.attackType.PHYSICAL, dmg = xi.damageType.SLASHING, primary = false, tpm = 1.0, bonus = 0, ttpm = 1.0 },
    { key = 'sub', slot = xi.slot.SUB, atk = xi.attackType.PHYSICAL, dmg = xi.damageType.PIERCING, primary = true, tpm = 1.0, bonus = 0, ttpm = 1.0 },
    { key = 'ranged', slot = xi.slot.RANGED, atk = xi.attackType.RANGED, dmg = xi.damageType.PIERCING, primary = true, tpm = 1.0, bonus = 50, ttpm = 1.0 },
    { key = 'magic', slot = xi.slot.MAIN, atk = xi.attackType.MAGICAL, dmg = xi.damageType.FIRE, primary = true, tpm = 1.0, bonus = 0, ttpm = 1.0 },
    { key = 'zero', slot = xi.slot.MAIN, atk = xi.attackType.PHYSICAL, dmg = xi.damageType.SLASHING, primary = true, tpm = 1.0, bonus = 0, ttpm = 1.0, amount = 0 },
}

golden.probeWs = function(p, m, c, emit)
    emit('p.delay', p:getBaseDelay())
    emit('p.rdelay', p:getBaseRangedDelay())
    emit('m.delay', m:getBaseDelay())
    for _, savetp in ipairs({ 0, 400 }) do
        p:setMod(xi.mod.SAVETP, savetp)
        for _, w in ipairs(wsConfigs) do
            local prefix = string.format('ws.%s.%d.', w.key, savetp)
            m:setHP(m:getMaxHP())
            m:setTP(0)
            p:setTP(0)
            m:resetEnmity(p)
            local amount = w.amount or 300
            emit(prefix .. 'pm', m:takeWeaponskillDamage(p, amount, w.atk, w.dmg, w.slot, w.primary, w.tpm, w.bonus, w.ttpm))
            emit(prefix .. 'pm.hp', m:getMaxHP() - m:getHP())
            emit(prefix .. 'pm.p.tp', p:getTP())
            emit(prefix .. 'pm.m.tp', m:getTP())
            emit(prefix .. 'pm.ce', m:getCE(p))
            emit(prefix .. 'pm.ve', m:getVE(p))

            p:setHP(p:getMaxHP())
            m:setTP(0)
            p:setTP(0)
            emit(prefix .. 'mp', p:takeWeaponskillDamage(m, math.floor(amount / 4), w.atk, w.dmg, xi.slot.MAIN, w.primary, w.tpm, w.bonus, w.ttpm))
            emit(prefix .. 'mp.hp', p:getMaxHP() - p:getHP())
            emit(prefix .. 'mp.m.tp', m:getTP())
            emit(prefix .. 'mp.p.tp', p:getTP())
        end
    end

    p:setMod(xi.mod.SAVETP, 0)
    m:setHP(m:getMaxHP())
    p:setHP(p:getMaxHP())
    m:setTP(0)
    p:setTP(0)
    m:resetEnmity(p)
end

-----------------------------------
-- Golden 3: the values only golden_hooks reaches (the capture's xi_test is built with capture/xi_test_hooks.patch,
-- the C# check registers the same table over its native ports)
-----------------------------------

-- the scripts' root (the capture runs in the server dir, the C# check sets it)
golden.serverRoot = golden.serverRoot or '.'

-- an action that takes every call (the scripts only record into it)
local function actionStub()
    return setmetatable({}, { __index = function()
        return function()
            return 0
        end
    end })
end

local function emitSwing(prefix, r, emit)
    emit(prefix .. 'dmg', r[1])
    emit(prefix .. 'sa', r[2])
    emit(prefix .. 'ta', r[3])
    emit(prefix .. 'ratio', r[4])
end

local nativeEffects =
{
    sa      = { { 'p', xi.effect.SNEAK_ATTACK } },
    sadoubt = { { 'p', xi.effect.SNEAK_ATTACK }, { 'm', xi.effect.DOUBT } },
    ta      = { { 'p', xi.effect.TRICK_ATTACK } },
    mighty  = { { 'p', xi.effect.MIGHTY_STRIKES } },
}

-- the native auto-attack cases: GetCritHitRate, GetHitRate, the first swing of a round (both ways) under each draw
-- sequence and MagicDmgTaken
golden.probeNative = function(p, m, c, emit)
    local gh = golden_hooks
    local fx = nativeEffects[c.native] or {}
    for _, e in ipairs(fx) do
        local target = e[1] == 'p' and p or m
        target:addStatusEffect(e[2], { power = 10, duration = 60, origin = p })
    end

    local target = p:getTarget()
    emit('nat.target', target and target:getID() == m:getID() or false)
    emit('nat.behind', p:isBehind(m, 64))
    emit('nat.facing.mp', m:isFacing(p))
    emit('nat.sa', p:hasStatusEffect(xi.effect.SNEAK_ATTACK))
    emit('nat.ta', p:hasStatusEffect(xi.effect.TRICK_ATTACK))
    emit('nat.trait.assassin', p:hasTrait(xi.trait.ASSASSIN))
    emit('p.wtype', p:getWeaponSkillType(xi.slot.MAIN))
    emit('p.DEX', p:getStat(xi.mod.DEX))
    emit('p.AGI', p:getStat(xi.mod.AGI))
    emit('m.AGI', m:getStat(xi.mod.AGI))

    for _, ignore in ipairs({ false, true }) do
        local k = ignore and 'ignore' or 'sata'
        emit('crit.native.pm.main.' .. k, gh.critHitRate(p, m, ignore, xi.slot.MAIN))
        emit('crit.native.pm.sub.' .. k, gh.critHitRate(p, m, ignore, xi.slot.SUB))
        emit('crit.native.mp.' .. k, gh.critHitRate(m, p, ignore, xi.slot.MAIN))
    end

    for attackNumber = 0, 2 do
        emit('hit.native.pm.' .. attackNumber, gh.hitRate(p, m, attackNumber, 0))
        emit('hit.native.mp.' .. attackNumber, gh.hitRate(m, p, attackNumber, 0))
    end

    emit('hit.native.pm.zanshin', gh.hitRate(p, m, 0, 35))
    emit('hit.native.pm.acc20', gh.hitRate(p, m, 0, 20))

    for _, mode in ipairs({ 'low', 'mid', 'high', 'mix3' }) do
        local f = golden.rngModes[mode]
        for _, crit in ipairs({ false, true }) do
            local ck = crit and 'crit' or 'norm'
            emitSwing(string.format('swing.pm.%s.%s.', ck, mode), golden.withRng(f, function()
                return { gh.swing(p, m, crit, f) }
            end), emit)
            emitSwing(string.format('swing.mp.%s.%s.', ck, mode), golden.withRng(f, function()
                return { gh.swing(m, p, crit, f) }
            end), emit)
        end
    end

    for _, dmg in ipairs({ 1, 100, 999, 2500 }) do
        for _, element in ipairs({ 0, 1, 4, 8 }) do
            emit(string.format('taken.magic.p.%d.%d', dmg, element), gh.magicDmgTaken(p, dmg, element))
            emit(string.format('taken.magic.m.%d.%d', dmg, element), gh.magicDmgTaken(m, dmg, element))
        end
    end

    for _, e in ipairs(fx) do
        local target = e[1] == 'p' and p or m
        target:delStatusEffectSilent(e[2])
    end
end

local castEffects =
{
    hasso       = { xi.effect.HASSO },
    seigan      = { xi.effect.SEIGAN },
    darkarts    = { xi.effect.DARK_ARTS },
    lightarts   = { xi.effect.LIGHT_ARTS },
    alacrity    = { xi.effect.DARK_ARTS, xi.effect.ALACRITY },
    celerity    = { xi.effect.LIGHT_ARTS, xi.effect.CELERITY },
    composure   = { xi.effect.COMPOSURE },
    nightingale = { xi.effect.NIGHTINGALE },
    troubadour  = { xi.effect.TROUBADOUR, xi.effect.NIGHTINGALE },
    pianissimo  = { xi.effect.PIANISSIMO },
}

-- the cast / recast cases: every spell's cast time (and Quick Magic's instant cast) and recast under the case's
-- effect and modifiers
golden.probeCast = function(p, m, c, emit)
    local fx = castEffects[c.castFx] or {}
    for _, effect in ipairs(fx) do
        p:addStatusEffect(effect, { power = 1, duration = 300, origin = p })
    end

    for _, effect in ipairs(fx) do
        emit('cast.fx.' .. effect, p:hasStatusEffect(effect))
    end

    for _, name in ipairs(c.cast) do
        local id = xi.magic.spell[name]
        local ms, instant = golden_hooks.castTime(p, id)
        emit('cast.' .. name, ms)
        emit('cast.' .. name .. '.instant', instant)
        emit('recast.' .. name, golden_hooks.recastTime(p, id))
    end

    for _, effect in ipairs(fx) do
        p:delStatusEffectSilent(effect)
    end
end

-- the area spell cases: the spell with the target count of its area (c.aoe other mobs moved next to the target),
-- its multiple target reduction and the damage it does under each draw sequence
golden.probeAoe = function(p, m, c, emit)
    local moved = {}
    for k = 1, c.aoe do
        local o = golden.findMob(golden.mobIds[k + 1])
        table.insert(moved, { o, o:getXPos(), o:getYPos(), o:getZPos(), o:getRotPos() })
        o:setPos(m:getXPos() + 0.75 * k, m:getYPos(), m:getZPos() + 0.5, o:getRotPos())
        -- in the caster's view, as the zone's spawn update would put it
        golden_hooks.seeMob(p, o, true)
    end

    for _, key in ipairs(c.spells) do
        local kind = golden.spellKinds[key]
        local spell, count = golden_hooks.spellTargets(p, m, xi.magic.spell[kind[2]])
        local prefix = 'aoe.' .. key .. '.'
        emit(prefix .. 'count', count)
        emit(prefix .. 'total', spell:getTotalTargets())
        emit(prefix .. 'mtdr', xi.spells.damage.calculateMTDR(p, spell))
        emit(prefix .. 'aoe', xi.spells.damage.calculateAreaOfEffectResistance(m, spell))
        for _, mode in ipairs({ 'low', 'mid', 'high', 'mix3' }) do
            m:setHP(m:getMaxHP())
            m:setTP(0)
            p:setTP(0)
            m:resetEnmity(p)
            emit(prefix .. mode .. '.dmg', golden.withRng(golden.rngModes[mode], function()
                return xi.spells.damage.useDamageSpell(p, m, spell)
            end))
            emit(prefix .. mode .. '.hp', m:getMaxHP() - m:getHP())
        end
    end

    for _, entry in ipairs(moved) do
        golden_hooks.seeMob(p, entry[1], false)
        entry[1]:setPos(entry[2], entry[3], entry[4], entry[5])
    end

    m:setHP(m:getMaxHP())
    m:setTP(0)
    m:resetEnmity(p)
end

local bpScripts = { [513] = 'poison_nails', [544] = 'punch', [547] = 'double_punch', [592] = 'claw', [656] = 'camisado' }

-- the blood pact cases: the avatar's pet ability script (onUseBloodPact, mobPhysicalMove, processDamage, takeDamage)
-- with a copy of the pet skill at each TP, under each draw sequence
golden.probeBp = function(p, pet, m, c, emit)
    emit('pet.id', pet:getPetID())
    emit('pet.lvl', pet:getMainLvl())
    emit('pet.ATT', pet:getStat(xi.mod.ATT))
    emit('pet.ACC', pet:getACC())
    emit('pet.wdmg', pet:getWeaponDmg())
    emit('pet.BP_DAMAGE', pet:getMod(xi.mod.BP_DAMAGE))
    for _, id in ipairs(c.bpSkills) do
        local script = dofile(golden.serverRoot .. '/scripts/actions/abilities/pets/' .. bpScripts[id] .. '.lua')
        for _, tp in ipairs({ 1000, 2000, 3000 }) do
            for _, mode in ipairs({ 'low', 'mid', 'high', 'mix3' }) do
                local prefix = string.format('bp.%d.%d.%s.', id, tp, mode)
                m:setHP(m:getMaxHP())
                m:setTP(0)
                pet:setTP(0)
                m:resetEnmity(pet)
                m:resetEnmity(p)
                local skill = golden_hooks.petSkill(id, tp)
                emit(prefix .. 'dmg', golden.withRng(golden.rngModes[mode], function()
                    return script.onPetAbility(m, pet, skill, p, actionStub())
                end))
                emit(prefix .. 'hp', m:getMaxHP() - m:getHP())
                emit(prefix .. 'msg', skill:getMsg())
                emit(prefix .. 'crit', skill:isCritical())
                emit(prefix .. 'atype', skill:getAttackType())
                emit(prefix .. 'pet.tp', pet:getTP())
                emit(prefix .. 'm.tp', m:getTP())
                emit(prefix .. 'ce', m:getCE(pet))
                emit(prefix .. 've', m:getVE(pet))
            end
        end
    end

    m:setHP(m:getMaxHP())
    m:setTP(0)
    pet:setTP(0)
    m:resetEnmity(pet)
    m:resetEnmity(p)
end

-- the weapon skill merit cases: the weapon skill's own script at each TP under each draw sequence
golden.probeWsm = function(p, m, c, emit)
    local name = string.upper(c.wsm)
    emit('merit.' .. name, p:getMerit(xi.merit[name]))
    emit('p.skill.main', p:getSkillLevel(p:getWeaponSkillType(xi.slot.MAIN)))
    local script = dofile(golden.serverRoot .. '/scripts/actions/weaponskills/' .. c.wsm .. '.lua')
    for _, tp in ipairs({ 1000, 2000, 3000 }) do
        for _, mode in ipairs({ 'low', 'mid', 'high', 'mix3' }) do
            local prefix = string.format('wsm.%d.%s.', tp, mode)
            m:setHP(m:getMaxHP())
            m:setTP(0)
            p:setTP(0)
            m:resetEnmity(p)
            local r = golden.withRng(golden.rngModes[mode], function()
                return { script.onUseWeaponSkill(p, m, xi.weaponskill[name], tp, true, actionStub(), nil) }
            end)
            emit(prefix .. 'tphits', r[1])
            emit(prefix .. 'extrahits', r[2])
            emit(prefix .. 'crit', r[3])
            emit(prefix .. 'dmg', r[4])
            emit(prefix .. 'hp', m:getMaxHP() - m:getHP())
            emit(prefix .. 'p.tp', p:getTP())
            emit(prefix .. 'm.tp', m:getTP())
            emit(prefix .. 'ce', m:getCE(p))
            emit(prefix .. 've', m:getVE(p))
        end
    end

    m:setHP(m:getMaxHP())
    m:setTP(0)
    p:setTP(0)
    m:resetEnmity(p)
end

-- the Flash cases: the attacker flashed (char and mob), the effect started the given milliseconds ago; the times
-- left (11020 .. 20 ms) sit 0.6 of a point above the floor, so the capture's few microseconds do not move them
golden.probeFlash = function(p, m, c, emit)
    local hr = xi.combat.physicalHitRate
    for _, who in ipairs({ { 'p', p, m }, { 'm', m, p } }) do
        local a, t = who[2], who[3]
        a:addStatusEffect(xi.effect.FLASH, { power = 200, duration = 12, origin = t })
        emit('flash.' .. who[1] .. '.eff', a:hasStatusEffect(xi.effect.FLASH))
        for _, elapsed in ipairs({ 980, 5980, 9980, 11980, 12500 }) do
            local k = string.format('flash.%s.%d.', who[1], elapsed)
            emit(k .. 'set', golden_hooks.setEffectElapsed(a, xi.effect.FLASH, elapsed))
            emit(k .. 'pen', hr.getFlashPenalty(a))
            local acc, eva = hr.getHitRateModifiers(a, t, false, false)
            emit(k .. 'acc', acc)
            emit(k .. 'eva', eva)
            emit(k .. 'hit', hr.getPhysicalHitRate(a, t, 0, 0, false))
            emit(k .. 'hit.native', golden_hooks.hitRate(a, t, 0, 0))
        end

        a:delStatusEffectSilent(xi.effect.FLASH)
    end
end

-- Runs one case: setup then probe; every value goes through emit(key, formatted string). charOf(i) answers char i of
-- golden.chars (the pet / cure cases' second char).
golden.run = function(p, c, emit, charOf)
    local m = golden.findMob(c.mob)
    if not m then
        error('golden: mob not found: ' .. tostring(c.mob))
    end

    if p:hasPet() then
        p:despawnPet()
    end

    golden.setupPlayer(p, c)
    golden.setupMob(m, c)
    local o = nil
    if c.cure then
        o = charOf(c.other)
        if o:hasPet() then
            o:despawnPet()
        end

        golden.setupPlayer(o, { job = xi.job.WAR, level = c.olevel, variant = 0 })
        o:setPos(m:getXPos() - 1, m:getYPos(), m:getZPos(), 0)
    end

    local pet = nil
    if c.pet then
        if c.job == xi.job.BST then
            p:setMod(xi.mod.JUG_LEVEL_RANGE, 2)
        end

        p:spawnPet(c.pet)
        p:setMod(xi.mod.JUG_LEVEL_RANGE, 0)
        pet = p:getPet()
        if not pet then
            error('golden: no pet: ' .. tostring(c.pet))
        end
    end

    if not c.fx and not c.pos then
        -- a yalm east of the target, within enmity and melee range
        p:setPos(m:getXPos() + 1, m:getYPos(), m:getZPos(), 0)
    else
        -- 1.5 yalms in front of or behind the target along its facing (rotation r: x + cos, z - sin of r * pi / 128),
        -- turned to face it
        local r = m:getRotPos() * math.pi / 128
        local d = c.pos == 'front' and 1.5 or -1.5
        p:setPos(m:getXPos() + d * math.cos(r), m:getYPos(), m:getZPos() - d * math.sin(r), 0)
        p:lookAt(m:getPos())
        if c.fx == 'signet' then
            p:addStatusEffect(xi.effect.SIGNET, { duration = 3600, origin = p })
        elseif c.fx == 'innin' then
            p:addStatusEffect(xi.effect.INNIN, { power = 30, subPower = 20, duration = 300, origin = p })
        elseif c.fx == 'yonin' then
            p:addStatusEffect(xi.effect.YONIN, { power = 30, subPower = 20, duration = 300, origin = p })
        end

        if c.engage then
            -- the native cases engage a target an earlier case may have left claimed by another char
            if c.native then
                m:updateClaim()
            end

            p:engage(m:getTargID())
        end
    end

    if pet then
        -- a yalm west of the target, facing it
        pet:setPos(m:getXPos() - 1, m:getYPos(), m:getZPos(), 0)
        pet:lookAt(m:getPos())
    end

    -- every draw the probe does not script itself (the 33% day / weather proc, ...) takes the middle fraction
    golden.withRng({ 0.5 }, function()
        local function emitText(k, v)
            emit(k, golden.fmt(v))
        end

        if c.native then
            golden.probeNative(p, m, c, emitText)
        elseif c.cast then
            golden.probeCast(p, m, c, emitText)
        elseif c.aoe then
            golden.probeAoe(p, m, c, emitText)
        elseif c.bpSkills then
            golden.probeBp(p, pet, m, c, emitText)
        elseif c.wsm then
            golden.probeWsm(p, m, c, emitText)
        elseif c.flash then
            golden.probeFlash(p, m, c, emitText)
        elseif c.merit then
            golden.probeMerit(p, m, c, emitText)
            if pet then
                golden.probePet(p, pet, m, c, function(k, v)
                    emitText('withpet.' .. k, v)
                end)
            end
        elseif pet then
            golden.probePet(p, pet, m, c, emitText)
        elseif c.cure then
            golden.probeCure(p, o, m, c, emitText)
        elseif c.abilities then
            golden.probeAbility(p, m, c, emitText)
        elseif c.spells then
            golden.probeSpell(p, m, c, emitText)
        elseif c.ws then
            golden.probeWs(p, m, c, emitText)
        elseif c.fx then
            golden.probeFx(p, m, c, emitText)
        else
            golden.probe(p, m, c, emitText)
        end
    end)
end

return golden
