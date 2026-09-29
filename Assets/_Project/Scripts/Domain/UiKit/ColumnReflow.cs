namespace PrincesPalace.Domain.UiKit
{
    // Closes the gap a hidden item leaves in a Column. The solver lays a
    // Column out once at build time, so hiding a child at runtime leaves its
    // slot empty; this says how far each remaining child moves up to take it.
    //
    // Works on the authored TOP edge of each child (top to bottom, y up), not
    // on sizes and spacing, so it cannot disagree with what the solver placed:
    // a hidden child's pitch is the distance from its top to the next child's
    // top (its height plus the spacing), and every later child moves up by the
    // pitches of the hidden children before it. Top edges, not centres: with
    // children of different heights the centre distance is not the space the
    // hidden child occupied.
    public static class ColumnReflow
    {
        public static float[] Shifts(float[] authoredTopY, bool[] visible)
        {
            var shifts = new float[authoredTopY.Length];
            float carried = 0f;

            for (int i = 0; i < authoredTopY.Length; i++)
            {
                shifts[i] = carried;

                // The last child has no successor and nothing after it to move.
                if (!visible[i] && i + 1 < authoredTopY.Length)
                {
                    carried += authoredTopY[i] - authoredTopY[i + 1];
                }
            }

            return shifts;
        }
    }
}
