using System.Runtime.CompilerServices;

// Contract B: the Editor assembly binds controller UI references by direct
// field assignment, which needs to see fields that are not part of the game's
// public surface.
//
// This replaces v1's SetField(controller, "fieldName", value) -- reflection over
// a STRING, used at 330 sites. A typo in that string produced no compile error
// and no runtime error; the field simply stayed null and the screen failed
// later, somewhere else, with a NullReferenceException that named the symptom
// rather than the cause. Direct assignment makes a typo or a type mismatch a
// compile error at the wiring site.
//
// Editor only, deliberately. PlayMode tests are NOT given this access: they
// drive the UI through scenes and public API like a player does, so a test can
// never quietly reach into controller state to make itself pass.
[assembly: InternalsVisibleTo("PrincesPalace.Editor")]
