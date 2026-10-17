namespace Fx
{
    public class FooResolver
    {
        public int Resolve(RawFooEntry raw) { return raw.id.Length + raw.readField; }
    }
}
