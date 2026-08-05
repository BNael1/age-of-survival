namespace AgeOfSurvival.Core.Persistence
{
    public static class GameSaveCodecLimits
    {
        public const int HeaderLength = 48;
        public const int HashLength = 32;
        public const int MaximumPayloadLength = 256 * 1024 * 1024;
        public const int MaximumStringByteLength = 4096;
        public const int MaximumDefinitions = 4096;
        public const int MaximumPlayerContainers = 4096;
        public const int MaximumEntriesPerContainer = 65536;
        public const int MaximumChunkMutations = 1000000;
        public const int MaximumHarvestedResourcesPerChunk = 65536;
        public const int MaximumGroundContainersPerChunk = 65536;
        public const int MaximumGroundItemsPerContainer = 65536;
    }
}
