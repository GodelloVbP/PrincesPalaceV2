using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // THE THREE PROOF SPELLS AS OBJECTS, so every EditMode suite that needs
    // one is looking at the same authoring rather than at its own paraphrase of
    // it.
    //
    // Written out field by field rather than parsed from skills.json on
    // purpose. Two of the three are not in skills.json at all -- the Water
    // block is M5's to author and the two-burst is deliberately fixture-only,
    // so no content or recipe register is touched to prove the scheduler
    // handles overlapping instances. Reading the file would make these tests
    // silently pass on a day the content had not been written yet.
    //
    // The numbers are the plan's, and the plan says they are first guesses to
    // be tuned against captures. A test asserting a POSITION or a SPEED here
    // would be pinning a tuning decision; what these are for is asserting the
    // model can EXPRESS them, and that the rules do not refuse them.
    internal static class SpellLayerFixtures
    {
        // The pilot: a compact core loop crossing the stage, a wake riding it,
        // droplets shed along the flight, a contact animation at arrival and a
        // second burst at the cue. Five layers, three renderer kinds, two
        // scopes and three schedule points.
        internal static SpellPresentation Water() => new SpellPresentation
        {
            layerFormat = 1,
            sfxPath = "Audio/Sfx/water_impact",
            hitCueSeconds = 0.35f,
            layers = new[]
            {
                new SpellLayer
                {
                    id = "core",
                    render = "sprite",
                    place = "caster-centre",
                    travelSeconds = 0.25f,
                    at = "release",
                    path = "Spells/prismatic_orb_water_core",
                    fps = 24f,
                    until = "loop",
                    size = 190f,
                    facing = "auto",
                    sort = "effects",
                },
                new SpellLayer
                {
                    id = "wake",
                    render = "still",
                    place = "layer:core",
                    follow = true,
                    at = "release",
                    path = "Spells/prismatic_orb_water_wake",
                    until = "hold",
                    fade = 0.10f,
                    dx = -70f,
                    scale = 0.85f,
                    impactX = 0.94f,
                    impactY = 0.50f,
                    facing = "auto",
                    sort = "effects",
                },
                new SpellLayer
                {
                    id = "shed",
                    render = "emitter",
                    place = "layer:core",
                    at = "release",
                    sort = "effects",
                    emitter = new SpellEmitter
                    {
                        path = "Spells/prismatic_orb_water_drops",
                        rate = 40f,
                        window = 0.25f,
                        sourceDx = -40f,
                        spreadDegrees = 55f,
                        aimDegrees = 200f,
                        speedMin = 90f,
                        speedMax = 220f,
                        inherit = 0.35f,
                        drag = 3.5f,
                        gravity = -1400f,
                        lifeMin = 0.22f,
                        lifeMax = 0.38f,
                        sizeMin = 0.35f,
                        sizeMax = 0.7f,
                        spinMin = -180f,
                        spinMax = 180f,
                        fadeFrom = 0.6f,
                        endScale = 0.8f,
                    },
                },
                new SpellLayer
                {
                    id = "splash",
                    render = "sprite",
                    place = "target-centre",
                    at = "arrival",
                    path = "Spells/prismatic_orb_water_contact",
                    startFrame = 2,
                    fps = 26f,
                    until = "once",
                    size = 300f,
                    facing = "auto",
                    sort = "effects",
                    impactX = 0.50f,
                    impactY = 0.50f,
                },
                new SpellLayer
                {
                    id = "spray",
                    render = "emitter",
                    place = "target-centre",
                    at = "hit",
                    sort = "effects",
                    emitter = new SpellEmitter
                    {
                        path = "Spells/prismatic_orb_water_drops",
                        burst = 18,
                        spreadDegrees = 150f,
                        aimDegrees = 70f,
                        speedMin = 160f,
                        speedMax = 520f,
                        drag = 2.0f,
                        gravity = -1600f,
                        lifeMin = 0.18f,
                        lifeMax = 0.34f,
                        sizeMin = 0.3f,
                        sizeMax = 0.9f,
                        spinMin = -360f,
                        spinMax = 360f,
                        fadeFrom = 0.55f,
                        endScale = 0.7f,
                    },
                },
            },
        };

        // The second validation case, and the one that proves the model
        // EXPRESSES what the adapter already produces: one shared ground
        // sequence, separate target plumes, both peaking on one instant, with
        // no spell id anywhere in orchestration.
        internal static SpellPresentation Cinderfault() => new SpellPresentation
        {
            layerFormat = 1,
            sfxPath = "Audio/Sfx/cinderfault_impact",
            castSfxPath = "Audio/Sfx/cinderfault_pressure",
            hitCueSeconds = 0.43333334f,
            layers = new[]
            {
                new SpellLayer
                {
                    id = "fault",
                    render = "sprite",
                    place = "formation",
                    at = "release",
                    path = "Spells/cinderfault_ground",
                    seconds = 0.78f,
                    until = "once",
                    impactY = 0.063f,
                    facing = "none",
                    sort = "ground",
                },
                new SpellLayer
                {
                    id = "erupt",
                    render = "sprite",
                    place = "target",
                    at = "release",
                    path = "Spells/cinderfault_eruption",
                    seconds = 0.78f,
                    until = "once",
                    impactX = 0.5f,
                    impactY = 0.129f,
                    facing = "none",
                    sort = "effects",
                },
            },
        };

        // TWO INSTANCES OF ONE RENDERER KIND, on a caster anchor, at different
        // offsets, with overlapping lifetimes -- B opens while A is 75% through.
        // Art that already ships (Resources/Vfx/impact_burst, six frames), so
        // nothing about this touches a recipe register.
        //
        // Neither layer authors sort, until or facing, deliberately: this is
        // also the case that pins the blank-word defaults.
        internal static SpellPresentation TwoBurst() => new SpellPresentation
        {
            layerFormat = 1,
            layers = new[]
            {
                new SpellLayer
                {
                    render = "sprite",
                    place = "caster-centre",
                    at = "release",
                    offset = 0f,
                    path = "Vfx/impact_burst",
                    seconds = 0.24f,
                    scale = 1.00f,
                },
                new SpellLayer
                {
                    render = "sprite",
                    place = "caster-centre",
                    at = "release",
                    offset = 0.18f,
                    path = "Vfx/impact_burst",
                    seconds = 0.24f,
                    scale = 1.35f,
                },
            },
        };
    }
}
