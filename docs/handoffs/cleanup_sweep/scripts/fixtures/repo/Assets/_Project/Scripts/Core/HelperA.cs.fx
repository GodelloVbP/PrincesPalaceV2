namespace Fx
{
    public class HelperA
    {
        private static int Clamp01(int v) { return v < 0 ? 0 : v; }
        public static int Spread(int v) { return v; }
    }
}
