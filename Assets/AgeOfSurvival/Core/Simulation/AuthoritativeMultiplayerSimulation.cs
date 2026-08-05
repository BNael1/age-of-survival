using System;
using System.Collections.Generic;
using System.Text;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Core.Simulation
{
    public enum AuthoritativeCommandRejection : byte
    {
        None = 0,
        UnknownClient = 1,
        InvalidSequence = 2,
        UnknownResource = 3,
        OutOfRange = 4,
        AlreadyHarvested = 5,
        InvalidIdentifier = 6
    }

    public readonly struct AuthoritativeCommandResult
    {
        public AuthoritativeCommandResult(
            bool accepted,
            AuthoritativeCommandRejection rejection,
            ulong digest)
        {
            Accepted = accepted;
            Rejection = rejection;
            Digest = digest;
        }

        public bool Accepted { get; }
        public AuthoritativeCommandRejection Rejection { get; }
        public ulong Digest { get; }
    }

    public readonly struct AuthoritativeWorldSnapshot : IEquatable<AuthoritativeWorldSnapshot>
    {
        public AuthoritativeWorldSnapshot(
            long revision,
            ResourceId resourceId,
            ResourceAvailability availability,
            int evictionCount,
            int restorationCount)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (evictionCount < 0) throw new ArgumentOutOfRangeException(nameof(evictionCount));
            if (restorationCount < 0) throw new ArgumentOutOfRangeException(nameof(restorationCount));
            if (!resourceId.IsValid) throw new ArgumentException("A snapshot requires a resource identifier.", nameof(resourceId));
            if (!Enum.IsDefined(typeof(ResourceAvailability), availability))
            {
                throw new ArgumentOutOfRangeException(nameof(availability));
            }

            Revision = revision;
            ResourceId = resourceId;
            Availability = availability;
            EvictionCount = evictionCount;
            RestorationCount = restorationCount;
            Digest = CalculateDigest(
                revision,
                resourceId.Value,
                availability,
                evictionCount,
                restorationCount);
        }

        public long Revision { get; }
        public ResourceId ResourceId { get; }
        public ResourceAvailability Availability { get; }
        public int EvictionCount { get; }
        public int RestorationCount { get; }
        public ulong Digest { get; }

        public static ulong CalculateDigest(
            long revision,
            string resourceId,
            ResourceAvailability availability,
            int evictionCount,
            int restorationCount)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (resourceId == null) throw new ArgumentNullException(nameof(resourceId));
            if (!Enum.IsDefined(typeof(ResourceAvailability), availability))
            {
                throw new ArgumentOutOfRangeException(nameof(availability));
            }

            if (evictionCount < 0) throw new ArgumentOutOfRangeException(nameof(evictionCount));
            if (restorationCount < 0) throw new ArgumentOutOfRangeException(nameof(restorationCount));

            byte[] bytes = Encoding.UTF8.GetBytes(resourceId);
            ulong hash = 14695981039346656037UL;
            AddUInt64(ref hash, unchecked((ulong)revision));
            AddUInt32(ref hash, checked((uint)bytes.Length));
            for (int index = 0; index < bytes.Length; index++)
            {
                AddByte(ref hash, bytes[index]);
            }

            AddByte(ref hash, (byte)availability);
            AddUInt32(ref hash, checked((uint)evictionCount));
            AddUInt32(ref hash, checked((uint)restorationCount));
            return hash;
        }

        private static void AddUInt64(ref ulong hash, ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                AddByte(ref hash, (byte)(value >> shift));
            }
        }

        private static void AddUInt32(ref ulong hash, uint value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                AddByte(ref hash, (byte)(value >> shift));
            }
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }

        public bool Equals(AuthoritativeWorldSnapshot other)
        {
            return Revision == other.Revision
                && ResourceId.Equals(other.ResourceId)
                && Availability == other.Availability
                && EvictionCount == other.EvictionCount
                && RestorationCount == other.RestorationCount
                && Digest == other.Digest;
        }

        public override bool Equals(object obj)
        {
            return obj is AuthoritativeWorldSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Revision.GetHashCode();
                hash = (hash * 397) ^ ResourceId.GetHashCode();
                hash = (hash * 397) ^ (int)Availability;
                hash = (hash * 397) ^ Digest.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class AuthoritativeMultiplayerSimulation
    {
        private readonly ChunkStateLifecycle _chunks;
        private readonly Dictionary<string, ClientState> _clients =
            new Dictionary<string, ClientState>(StringComparer.Ordinal);
        private readonly ChunkCoordinate _targetChunk;
        private int _evictionCount;
        private int _restorationCount;
        private long _revision;

        public AuthoritativeMultiplayerSimulation(WorldSeed seed)
        {
            Seed = seed;
            var generator = new DeterministicWorldPopulationGenerator(
                WorldPopulationDefaults.CreateTemperatePrototypeV1(seed));
            _chunks = new ChunkStateLifecycle(generator);

            ActiveChunkState targetState = null;
            for (long ring = 0; ring <= 8 && targetState == null; ring++)
            {
                for (long y = -ring; y <= ring && targetState == null; y++)
                {
                    for (long x = -ring; x <= ring; x++)
                    {
                        if (Math.Max(Math.Abs(x), Math.Abs(y)) != ring) continue;
                        var coordinate = new ChunkCoordinate(x, y);
                        ActiveChunkState candidate = _chunks.Activate(coordinate);
                        if (candidate.Resources.Count > 0)
                        {
                            targetState = candidate;
                            break;
                        }

                        _chunks.Evict(coordinate);
                    }
                }
            }

            if (targetState == null)
            {
                throw new InvalidOperationException("No generated resource was found for the multiplayer slice.");
            }

            _targetChunk = targetState.Coordinate;
            TargetResourceId = targetState.Resources[0].Id;
            TargetPosition = targetState.Resources[0].Position;
        }

        public WorldSeed Seed { get; }
        public ResourceId TargetResourceId { get; }
        public WorldPosition TargetPosition { get; }
        public int ConnectedClientCount => _clients.Count;

        public void Connect(string clientId)
        {
            StableIdentifierValidation.Validate(clientId, nameof(clientId));
            if (clientId.Length > 64)
            {
                throw new ArgumentOutOfRangeException(nameof(clientId), "Client identifiers are limited to 64 characters.");
            }

            if (_clients.ContainsKey(clientId))
            {
                throw new InvalidOperationException("The client identifier is already connected.");
            }

            _clients.Add(clientId, new ClientState(TargetPosition));
        }

        public bool Disconnect(string clientId)
        {
            return clientId != null && _clients.Remove(clientId);
        }

        public AuthoritativeCommandResult Harvest(
            string clientId,
            uint sequence,
            ResourceId requestedResource)
        {
            AuthoritativeWorldSnapshot before = CreateSnapshot();
            if (clientId == null || !_clients.TryGetValue(clientId, out ClientState client))
            {
                return Reject(AuthoritativeCommandRejection.UnknownClient, before.Digest);
            }

            if (sequence == 0 || sequence <= client.LastSequence)
            {
                return Reject(AuthoritativeCommandRejection.InvalidSequence, before.Digest);
            }

            client.LastSequence = sequence;
            if (!requestedResource.IsValid)
            {
                return Reject(AuthoritativeCommandRejection.InvalidIdentifier, before.Digest);
            }

            ActiveChunkState active = _chunks.GetOrActivate(_targetChunk);
            ResourceState requested = active.FindResource(requestedResource);
            if (requested == null)
            {
                return Reject(AuthoritativeCommandRejection.UnknownResource, before.Digest);
            }

            if (client.Position.DistanceSquaredTo(requested.Position) > 0d)
            {
                return Reject(AuthoritativeCommandRejection.OutOfRange, before.Digest);
            }

            if (requested.Availability != ResourceAvailability.Available)
            {
                return Reject(AuthoritativeCommandRejection.AlreadyHarvested, before.Digest);
            }

            ResourceInteractionResult result = ResourceInteraction.Apply(
                default,
                active.Resources,
                client.Position,
                0d);
            if (!result.Succeeded || !result.HarvestedResourceId.Value.Equals(requestedResource))
            {
                throw new InvalidOperationException("The authoritative interaction selected an unexpected resource.");
            }

            _revision++;
            AuthoritativeWorldSnapshot beforeEviction = CreateSnapshot();
            _chunks.Evict(_targetChunk);
            _evictionCount++;
            _chunks.Activate(_targetChunk);
            _restorationCount++;
            AuthoritativeWorldSnapshot restored = CreateSnapshot();
            if (restored.Revision != beforeEviction.Revision
                || !restored.ResourceId.Equals(beforeEviction.ResourceId)
                || restored.Availability != beforeEviction.Availability
                || restored.EvictionCount != beforeEviction.EvictionCount + 1
                || restored.RestorationCount != beforeEviction.RestorationCount + 1)
            {
                throw new InvalidOperationException(
                    "Server eviction failed to preserve gameplay state or update lifecycle counters exactly once.");
            }

            return new AuthoritativeCommandResult(true, AuthoritativeCommandRejection.None, restored.Digest);
        }

        public AuthoritativeWorldSnapshot CreateSnapshot()
        {
            ActiveChunkState active = _chunks.GetOrActivate(_targetChunk);
            ResourceState target = active.FindResource(TargetResourceId);
            return new AuthoritativeWorldSnapshot(
                _revision,
                TargetResourceId,
                target.Availability,
                _evictionCount,
                _restorationCount);
        }

        private static AuthoritativeCommandResult Reject(
            AuthoritativeCommandRejection rejection,
            ulong digest)
        {
            return new AuthoritativeCommandResult(false, rejection, digest);
        }

        private sealed class ClientState
        {
            public ClientState(WorldPosition position)
            {
                Position = position;
            }

            public WorldPosition Position { get; }
            public uint LastSequence { get; set; }
        }
    }
}
