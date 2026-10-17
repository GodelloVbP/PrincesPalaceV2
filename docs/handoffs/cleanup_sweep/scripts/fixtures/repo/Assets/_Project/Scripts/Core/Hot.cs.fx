namespace Fx
{
    public class Hot
    {
        void Update()
        {
            if (a) { var s = "}"; Foo(); } // }
            var l = new List<int>();
            if (b) { x = GetComponent<Foo>(); }
        }

        void Other()
        {
            var q = new List<int>();
        }
    }
}
