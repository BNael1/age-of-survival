using System;
using System.IO;
using System.Text;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.Simulation;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Protocol
{
    public static class MultiplayerProtocol
    {
        public const uint Magic = 0x314F5341;
        public const ushort Version = 1;
        public const int HeaderSize = 10;
        public const int MaximumMessageSize = 1024;
        public const int MaximumClientIdLength = 64;
        public const int MaximumBuildVersionLength = 64;

        public static bool IsValidEncodedSize(int length)
        {
            return length >= HeaderSize && length <= MaximumMessageSize;
        }

        public static byte[] Encode(ProtocolMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            byte[] payload;
            using (var payloadStream = new MemoryStream())
            using (var writer = new BinaryWriter(payloadStream, Encoding.UTF8, true))
            {
                WritePayload(writer, message);
                payload = payloadStream.ToArray();
            }

            if (payload.Length + HeaderSize > MaximumMessageSize)
            {
                throw new InvalidOperationException("The protocol message exceeds its size limit.");
            }

            using (var stream = new MemoryStream(HeaderSize + payload.Length))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write((byte)message.Type);
                writer.Write((byte)0);
                writer.Write(checked((ushort)payload.Length));
                writer.Write(payload);
                return stream.ToArray();
            }
        }

        public static ProtocolDecodeResult TryDecode(byte[] data, out ProtocolMessage message)
        {
            message = null;
            if (data == null || !IsValidEncodedSize(data.Length))
            {
                return ProtocolDecodeResult.InvalidSize;
            }

            try
            {
                using (var stream = new MemoryStream(data, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    if (reader.ReadUInt32() != Magic) return ProtocolDecodeResult.InvalidMagic;
                    ushort version = reader.ReadUInt16();
                    if (version != Version) return ProtocolDecodeResult.IncompatibleVersion;
                    ProtocolMessageType type = (ProtocolMessageType)reader.ReadByte();
                    if (reader.ReadByte() != 0) return ProtocolDecodeResult.InvalidPayload;
                    int payloadLength = reader.ReadUInt16();
                    if (payloadLength != data.Length - HeaderSize) return ProtocolDecodeResult.InvalidSize;
                    if (!Enum.IsDefined(typeof(ProtocolMessageType), type)) return ProtocolDecodeResult.UnknownMessage;

                    message = ReadPayload(reader, type);
                    if (stream.Position != stream.Length)
                    {
                        message = null;
                        return ProtocolDecodeResult.TrailingData;
                    }

                    return ProtocolDecodeResult.Success;
                }
            }
            catch (Exception exception) when (
                exception is EndOfStreamException
                || exception is ArgumentException
                || exception is IOException
                || exception is OverflowException)
            {
                message = null;
                return ProtocolDecodeResult.InvalidPayload;
            }
        }

        private static void WritePayload(BinaryWriter writer, ProtocolMessage message)
        {
            switch (message.Type)
            {
                case ProtocolMessageType.Hello:
                    WriteString(writer, message.ClientId, MaximumClientIdLength);
                    WriteString(writer, message.BuildVersion, MaximumBuildVersionLength);
                    break;
                case ProtocolMessageType.Welcome:
                    WriteString(writer, message.BuildVersion, MaximumBuildVersionLength);
                    break;
                case ProtocolMessageType.Snapshot:
                    writer.Write(message.Revision);
                    WriteString(writer, message.ResourceId, StableIdentifierValidation.MaximumLength);
                    writer.Write((byte)message.Availability);
                    writer.Write(message.EvictionCount);
                    writer.Write(message.RestorationCount);
                    writer.Write(message.Digest);
                    break;
                case ProtocolMessageType.HarvestIntent:
                    writer.Write(message.Sequence);
                    WriteString(writer, message.ResourceId, StableIdentifierValidation.MaximumLength);
                    break;
                case ProtocolMessageType.CommandRejected:
                    writer.Write(message.Sequence);
                    writer.Write((byte)message.Rejection);
                    writer.Write(message.Digest);
                    break;
                case ProtocolMessageType.ClientComplete:
                    writer.Write(message.Digest);
                    break;
                case ProtocolMessageType.Ready:
                case ProtocolMessageType.ScenarioStart:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(message), message.Type, "Unknown protocol message type.");
            }
        }

        private static ProtocolMessage ReadPayload(BinaryReader reader, ProtocolMessageType type)
        {
            switch (type)
            {
                case ProtocolMessageType.Hello:
                    return ProtocolMessage.Hello(
                        ReadString(reader, MaximumClientIdLength),
                        ReadString(reader, MaximumBuildVersionLength));
                case ProtocolMessageType.Welcome:
                    return ProtocolMessage.Welcome(ReadString(reader, MaximumBuildVersionLength));
                case ProtocolMessageType.Snapshot:
                    return ProtocolMessage.Snapshot(
                        reader.ReadInt64(),
                        ReadString(reader, StableIdentifierValidation.MaximumLength),
                        (ResourceAvailability)reader.ReadByte(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadUInt64());
                case ProtocolMessageType.Ready:
                    return ProtocolMessage.Ready();
                case ProtocolMessageType.ScenarioStart:
                    return ProtocolMessage.ScenarioStart();
                case ProtocolMessageType.HarvestIntent:
                    return ProtocolMessage.HarvestIntent(
                        reader.ReadUInt32(),
                        ReadString(reader, StableIdentifierValidation.MaximumLength));
                case ProtocolMessageType.CommandRejected:
                    return ProtocolMessage.CommandRejected(
                        reader.ReadUInt32(),
                        (AuthoritativeCommandRejection)reader.ReadByte(),
                        reader.ReadUInt64());
                case ProtocolMessageType.ClientComplete:
                    return ProtocolMessage.ClientComplete(reader.ReadUInt64());
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        private static void WriteString(BinaryWriter writer, string value, int maximumCharacters)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximumCharacters)
            {
                throw new ArgumentException("A protocol string is empty or exceeds its character limit.", nameof(value));
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]))
                {
                    throw new ArgumentException(
                        "A protocol string contains control characters.",
                        nameof(value));
                }
            }

            byte[] encoded = Encoding.UTF8.GetBytes(value);
            if (encoded.Length > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            writer.Write((ushort)encoded.Length);
            writer.Write(encoded);
        }

        private static string ReadString(BinaryReader reader, int maximumCharacters)
        {
            int byteLength = reader.ReadUInt16();
            if (byteLength == 0 || byteLength > MaximumMessageSize)
            {
                throw new ArgumentException("A protocol string has an invalid byte length.");
            }

            byte[] bytes = reader.ReadBytes(byteLength);
            if (bytes.Length != byteLength) throw new EndOfStreamException();
            string value = new UTF8Encoding(false, true).GetString(bytes);
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximumCharacters)
            {
                throw new ArgumentException("A protocol string exceeds its character limit.");
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]))
                {
                    throw new ArgumentException("A protocol string contains control characters.");
                }
            }

            return value;
        }
    }

    public enum ProtocolMessageType : byte
    {
        Hello = 1,
        Welcome = 2,
        Snapshot = 3,
        Ready = 4,
        ScenarioStart = 5,
        HarvestIntent = 6,
        CommandRejected = 7,
        ClientComplete = 8
    }

    public enum ProtocolDecodeResult : byte
    {
        Success = 0,
        InvalidSize = 1,
        InvalidMagic = 2,
        IncompatibleVersion = 3,
        UnknownMessage = 4,
        InvalidPayload = 5,
        TrailingData = 6
    }

    public sealed class ProtocolMessage
    {
        private ProtocolMessage(ProtocolMessageType type)
        {
            Type = type;
        }

        public ProtocolMessageType Type { get; }
        public string ClientId { get; private set; }
        public string BuildVersion { get; private set; }
        public long Revision { get; private set; }
        public string ResourceId { get; private set; }
        public ResourceAvailability Availability { get; private set; }
        public int EvictionCount { get; private set; }
        public int RestorationCount { get; private set; }
        public ulong Digest { get; private set; }
        public uint Sequence { get; private set; }
        public AuthoritativeCommandRejection Rejection { get; private set; }

        public static ProtocolMessage Hello(string clientId, string buildVersion)
        {
            return new ProtocolMessage(ProtocolMessageType.Hello)
            {
                ClientId = clientId,
                BuildVersion = buildVersion
            };
        }

        public static ProtocolMessage Welcome(string buildVersion)
        {
            return new ProtocolMessage(ProtocolMessageType.Welcome) { BuildVersion = buildVersion };
        }

        public static ProtocolMessage Snapshot(
            long revision,
            string resourceId,
            ResourceAvailability availability,
            int evictionCount,
            int restorationCount,
            ulong digest)
        {
            if (revision < 0 || evictionCount < 0 || restorationCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            if (!Enum.IsDefined(typeof(ResourceAvailability), availability))
            {
                throw new ArgumentOutOfRangeException(nameof(availability));
            }

            return new ProtocolMessage(ProtocolMessageType.Snapshot)
            {
                Revision = revision,
                ResourceId = resourceId,
                Availability = availability,
                EvictionCount = evictionCount,
                RestorationCount = restorationCount,
                Digest = digest
            };
        }

        public static ProtocolMessage Ready() => new ProtocolMessage(ProtocolMessageType.Ready);
        public static ProtocolMessage ScenarioStart() => new ProtocolMessage(ProtocolMessageType.ScenarioStart);

        public static ProtocolMessage HarvestIntent(uint sequence, string resourceId)
        {
            if (sequence == 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            return new ProtocolMessage(ProtocolMessageType.HarvestIntent)
            {
                Sequence = sequence,
                ResourceId = resourceId
            };
        }

        public static ProtocolMessage CommandRejected(
            uint sequence,
            AuthoritativeCommandRejection rejection,
            ulong digest)
        {
            if (sequence == 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (rejection == AuthoritativeCommandRejection.None
                || !Enum.IsDefined(typeof(AuthoritativeCommandRejection), rejection))
            {
                throw new ArgumentOutOfRangeException(nameof(rejection));
            }

            return new ProtocolMessage(ProtocolMessageType.CommandRejected)
            {
                Sequence = sequence,
                Rejection = rejection,
                Digest = digest
            };
        }

        public static ProtocolMessage ClientComplete(ulong digest)
        {
            return new ProtocolMessage(ProtocolMessageType.ClientComplete) { Digest = digest };
        }
    }

    public sealed class ReplicatedWorldState
    {
        public long Revision { get; private set; } = -1;
        public string ResourceId { get; private set; }
        public ResourceAvailability Availability { get; private set; }
        public int EvictionCount { get; private set; }
        public int RestorationCount { get; private set; }
        public ulong Digest { get; private set; }

        public void Apply(ProtocolMessage snapshot)
        {
            if (snapshot == null || snapshot.Type != ProtocolMessageType.Snapshot)
            {
                throw new ArgumentException("A replicated state requires a snapshot message.", nameof(snapshot));
            }

            if (snapshot.Revision < Revision)
            {
                throw new InvalidOperationException("A stale snapshot cannot replace newer replicated state.");
            }

            if (snapshot.Revision == Revision
                && Revision >= 0
                && (snapshot.ResourceId != ResourceId
                    || snapshot.Availability != Availability
                    || snapshot.EvictionCount != EvictionCount
                    || snapshot.RestorationCount != RestorationCount
                    || snapshot.Digest != Digest))
            {
                throw new InvalidOperationException(
                    "A snapshot cannot rewrite an existing revision with different state.");
            }

            ulong calculated = AuthoritativeWorldSnapshot.CalculateDigest(
                snapshot.Revision,
                snapshot.ResourceId,
                snapshot.Availability,
                snapshot.EvictionCount,
                snapshot.RestorationCount);
            if (calculated != snapshot.Digest)
            {
                throw new InvalidOperationException("The replicated snapshot digest is invalid.");
            }

            Revision = snapshot.Revision;
            ResourceId = snapshot.ResourceId;
            Availability = snapshot.Availability;
            EvictionCount = snapshot.EvictionCount;
            RestorationCount = snapshot.RestorationCount;
            Digest = snapshot.Digest;
        }
    }
}
