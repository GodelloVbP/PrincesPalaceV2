using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // THE THREE PROOF SPELLS AS OBJECTS, so every EditMode suite that needs
    // one is looking at the same authoring rather than at its own paraphrase of
    // it.
    //
    // Written out field by field rather than parsed from skills.json on
    // purpose. The two-burst is deliberately fixture-only, so no content or
    // recipe register is touched to prove the scheduler handles overlapping
    // instances; and reading the file would make every one of these tests
    // silently pass on a day the content had been deleted.
    //
    // WATER IS NOW ALSO IN skills.json, which it was not when this file was
    // written, and the two are kept the same by hand rather than by a parse.
    // That is a real risk and it is bounded: SpellPoolCapacityTests reads the
    // authored block out of skills.json through SpellVfxJson and holds the
    // SHIPPED spell to the same three bands, so a fixture that drifted would
    // stop describing the game without stopping the game from being checked.
    // What the fixture is still for is the questions the file cannot answer --
    // that the model EXPRESSES this composition and that the rules do not
    // refuse it.
    //
    // A test asserting a POSITION or a SPEED here would be pinning a tuning
    // decision, which these numbers are: the plan's first guesses, corrected in
    // M5 where a measurement replaced a guess.
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

                    // MEASURED, NOT THE PLAN'S -70/0.94/0.50. A follower's
                    // position is its SOURCE's plus (dx, dy), so the plan's
                    // impactX/impactY on this layer never reached a placement;
                    // dx is the field that attaches the wake. Against the
                    // cropped sheet's own profile -- ink peaks at 0.87 of the
                    // width and the box renders 266 wide at scale 0.7 -- -118
                    // puts the thick end just behind the core's centre and
                    // trails the rest away from it.
                    dx = -118f,
                    scale = 0.7f,
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
                        sizeMin = 0.5f,
                        sizeMax = 1.0f,
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
                    size = 260f,
                    facing = "auto",
                    sort = "effects",

                    // MEASURED OFF THE CUT FRAMES rather than assumed centred.
                    // The crown's spine -- the densest column of f1, f3 and f4
                    // alike -- sits at 0.60 of the frame with the fan opening
                    // to its left, and the ink's vertical centroid at 0.48. Put
                    // 0.5 on the target and the crown straddles the body; put
                    // 0.60 there and it opens across the near contact surface,
                    // which is the side the ball arrives on.
                    impactX = 0.76f,
                    impactY = 0.58f,
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

                        // 115 RATHER THAN THE PLAN'S 70, to agree with the art
                        // it fires alongside: the contact crown opens backwards
                        // and up, so a cone centred forward-and-up threw the
                        // spray the opposite way from the drawing.
                        aimDegrees = 115f,
                        speedMin = 160f,
                        speedMax = 520f,
                        drag = 2.0f,
                        gravity = -1600f,
                        lifeMin = 0.18f,
                        lifeMax = 0.34f,
                        sizeMin = 0.45f,
                        sizeMax = 1.15f,
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
