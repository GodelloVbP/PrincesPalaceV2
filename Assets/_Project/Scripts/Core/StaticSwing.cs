using System.Collections;
using UnityEngine;

namespace PrincesPalace
{
    // A SINGLE-DRAWING ACTOR'S SWING, given the wind-up its art cannot draw.
    //
    // Every stance in the game is one drawing (docs/STANCE_SHEET_SPEC.md), so
    // there are no frames to spend time on and the impact instant -- the
    // flash, the recoil, the squash, the damage number -- would otherwise fire
    // about one frame after Lunge() started its outbound tween. The blow would
    // land before the attacker had crossed.
    //
    // ANTICIPATION AND THE IMPACT INSTANT MOVE TOGETHER, which is why the two
    // numbers are read from one place. Adding a crouch in front of the lunge
    // (StageActorAnimator's leadSeconds) widens exactly that gap, so the only
    // safe way to have one is for the wind-up to REPORT the whole travel:
    // anticipation plus the outbound tween. FightBeatPlayer waits it out
    // before firing the impact, and the two cannot drift apart because there
    // is one number.
    //
    // A static class rather than an object, because there is nothing
    // per-actor to hold: the wind-up is the same crouch and the same crossing
    // whoever is swinging. The stance's own drawing is put on by
    // FightController.PoseCombatant before the beat opens and taken off after
    // it, so nothing here touches a sprite.
    public static class StaticSwing
    {
        // The crouch plus the travel. Both numbers live on StageActorAnimator
        // beside the tween that actually spends them; this is the reader, not
        // a second home.
        public static float WindupSeconds =>
            StageActorAnimator.AnticipationSeconds + StageActorAnimator.LungeSeconds;

        public static IEnumerator Windup()
        {
            // AT THE TOP OF THE CROUCH, not at contact. The swell has to be
            // audible before the blow or it reads as a second impact sound.
            SoundController.PlayClip(ContactCues.WhooshClipPath);

            yield return new WaitForSeconds(FightBeatPlayer.Scaled(WindupSeconds));
        }
    }
}
