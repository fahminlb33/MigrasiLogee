namespace MigrasiLogee.Helpers
{
    public static class NetworkHelpers
    {
        public const string DefaultDnsResolverAddress = "8.8.8.8";

        public const int HttpPort = 80;
        public const int HttpsPort = 443;

        public const int LocalMongoPort = 27099;
        public const int RemoteMongoPort = 27017;
        public static readonly string ForwardedMongoHost = $"localhost:{LocalMongoPort}";
    }
}
