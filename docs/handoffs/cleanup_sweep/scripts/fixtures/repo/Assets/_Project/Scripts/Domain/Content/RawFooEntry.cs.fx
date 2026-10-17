using System;

namespace Fx
{
    [Serializable]
    public class RawFooEntry
    {
        public string id;
        public int readField;
        public int unreadField;
    }

    [Serializable]
    public class RawFooFile
    {
        public RawFooEntry[] foo;
    }
}
