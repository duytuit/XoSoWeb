using System.Threading;

namespace XoSoWeb.Services
{
    // Writers in this process call Invalidate after committing. External collectors still need integration/TTL.
    public static class PublicContentVersion
    {
        private static long version;
        public static long Value => Interlocked.Read(ref version);
        public static void Invalidate() => Interlocked.Increment(ref version);
    }
}
