namespace PrincesPalace.Domain.Combat.Session
{
    // HOW FAR A BLOW TRAVELS TO REACH WHAT IT HITS.
    //
    // The vocabulary a skill authors and the view obeys, in Domain because it
    // is a property of the ACTION -- a slam is a slam whether or not the actor
    // has art -- and because the alternative was the view guessing from a
    // stance name.
    public enum StageApproach
    {
        // Rooted. A cast, and any plain attack whose art is itself a stationary
        // pose (the Golem's ground slam, whose "attack" stance is a byte-for-
        // byte alias of its "cast" one).
        Hold = 0,

        // Leans in. A fraction of the way to the target and back -- the
        // ordinary melee swing, and what every plain attack has always done.
        Lunge = 1,

        // Closes the distance FIRST, then strikes, then walks it back.
        //
        // The difference from Lunge is not the distance, it is the ORDER: a
        // lunge is the swing, so the two happen at once, while this arrives
        // before the stance so much as begins. That is what "step in front of
        // it and then bring the thing down" looks like, and it is the only one
        // of the three that costs the beat any extra time.
        Close = 2,
    }
}
