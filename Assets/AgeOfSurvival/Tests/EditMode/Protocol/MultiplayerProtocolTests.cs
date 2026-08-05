using System;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.Simulation;
using AgeOfSurvival.Core.World.Generation;
using NUnit.Framework;

namespace AgeOfSurvival.Protocol.Tests
{
    public sealed class MultiplayerProtocolTests
    {
        [Test]
        public void TwoClientsShareInitialAndMutatedAuthoritativeState()
        {
            var simulation = new AuthoritativeMultiplayerSimulation(new WorldSeed(0));
            simulation.Connect("client-a");
            simulation.Connect("client-b");
            AuthoritativeWorldSnapshot initialA = simulation.CreateSnapshot();
            AuthoritativeWorldSnapshot initialB = simulation.CreateSnapshot();
            Assert.That(initialB, Is.EqualTo(initialA));
            Assert.That(initialA.Availability, Is.EqualTo(ResourceAvailability.Available));

            AuthoritativeCommandResult result = simulation.Harvest(
                "client-a",
                1,
                simulation.TargetResourceId);
            AuthoritativeWorldSnapshot mutatedA = simulation.CreateSnapshot();
            AuthoritativeWorldSnapshot mutatedB = simulation.CreateSnapshot();

            Assert.That(result.Accepted, Is.True);
            Assert.That(mutatedB, Is.EqualTo(mutatedA));
            Assert.That(mutatedA.Availability, Is.EqualTo(ResourceAvailability.Harvested));
            Assert.That(mutatedA.EvictionCount, Is.EqualTo(1));
            Assert.That(mutatedA.RestorationCount, Is.EqualTo(1));
        }

        [Test]
        public void InvalidCommandIsRejectedWithoutStateChange()
        {
            var simulation = new AuthoritativeMultiplayerSimulation(new WorldSeed(0));
            simulation.Connect("client-a");
            Assert.That(simulation.Harvest("client-a", 1, simulation.TargetResourceId).Accepted, Is.True);
            AuthoritativeWorldSnapshot before = simulation.CreateSnapshot();

            AuthoritativeCommandResult rejected = simulation.Harvest(
                "client-a",
                2,
                simulation.TargetResourceId);

            Assert.That(rejected.Accepted, Is.False);
            Assert.That(rejected.Rejection, Is.EqualTo(AuthoritativeCommandRejection.AlreadyHarvested));
            Assert.That(rejected.Digest, Is.EqualTo(before.Digest));
            Assert.That(simulation.CreateSnapshot(), Is.EqualTo(before));
        }

        [Test]
        public void UnknownResourceAndReplayedSequenceAreRejected()
        {
            var simulation = new AuthoritativeMultiplayerSimulation(new WorldSeed(0));
            simulation.Connect("client-a");
            AuthoritativeWorldSnapshot before = simulation.CreateSnapshot();

            AuthoritativeCommandResult unknown = simulation.Harvest(
                "client-a",
                1,
                new ResourceId("unknown"));
            AuthoritativeCommandResult replay = simulation.Harvest(
                "client-a",
                1,
                simulation.TargetResourceId);

            Assert.That(unknown.Rejection, Is.EqualTo(AuthoritativeCommandRejection.UnknownResource));
            Assert.That(replay.Rejection, Is.EqualTo(AuthoritativeCommandRejection.InvalidSequence));
            Assert.That(simulation.CreateSnapshot(), Is.EqualTo(before));
        }

        [Test]
        public void ReconnectedClientReceivesTheSameDigest()
        {
            var simulation = new AuthoritativeMultiplayerSimulation(new WorldSeed(0));
            simulation.Connect("client-a");
            simulation.Harvest("client-a", 1, simulation.TargetResourceId);
            ulong digest = simulation.CreateSnapshot().Digest;

            Assert.That(simulation.Disconnect("client-a"), Is.True);
            simulation.Connect("client-a");

            Assert.That(simulation.CreateSnapshot().Digest, Is.EqualTo(digest));
        }

        [Test]
        public void AllMessageKindsRoundTripCanonically()
        {
            ProtocolMessage[] messages =
            {
                ProtocolMessage.Hello("client-a", "build-a"),
                ProtocolMessage.Welcome("server-a"),
                ProtocolMessage.Snapshot(1, "resource-a", ResourceAvailability.Harvested, 1, 1, 123UL),
                ProtocolMessage.Ready(),
                ProtocolMessage.ScenarioStart(),
                ProtocolMessage.HarvestIntent(1, "resource-a"),
                ProtocolMessage.CommandRejected(2, AuthoritativeCommandRejection.AlreadyHarvested, 123UL),
                ProtocolMessage.ClientComplete(123UL)
            };

            for (int index = 0; index < messages.Length; index++)
            {
                byte[] encoded = MultiplayerProtocol.Encode(messages[index]);
                ProtocolDecodeResult result = MultiplayerProtocol.TryDecode(encoded, out ProtocolMessage decoded);
                Assert.That(result, Is.EqualTo(ProtocolDecodeResult.Success));
                Assert.That(decoded.Type, Is.EqualTo(messages[index].Type));
                Assert.That(MultiplayerProtocol.Encode(decoded), Is.EqualTo(encoded));
            }
        }

        [Test]
        public void IncompatibleProtocolVersionIsRefusedCleanly()
        {
            byte[] encoded = MultiplayerProtocol.Encode(ProtocolMessage.Ready());
            encoded[4] = 2;
            encoded[5] = 0;

            ProtocolDecodeResult result = MultiplayerProtocol.TryDecode(encoded, out ProtocolMessage message);

            Assert.That(result, Is.EqualTo(ProtocolDecodeResult.IncompatibleVersion));
            Assert.That(message, Is.Null);
        }

        [Test]
        public void UnknownAndTrailingMessagesAreRefused()
        {
            byte[] unknown = MultiplayerProtocol.Encode(ProtocolMessage.Ready());
            unknown[6] = 255;
            Assert.That(
                MultiplayerProtocol.TryDecode(unknown, out _),
                Is.EqualTo(ProtocolDecodeResult.UnknownMessage));

            byte[] ready = MultiplayerProtocol.Encode(ProtocolMessage.Ready());
            var trailing = new byte[ready.Length + 1];
            Array.Copy(ready, trailing, ready.Length);
            trailing[8] = 1;
            trailing[9] = 0;
            trailing[trailing.Length - 1] = 42;
            Assert.That(
                MultiplayerProtocol.TryDecode(trailing, out _),
                Is.EqualTo(ProtocolDecodeResult.TrailingData));
        }

        [Test]
        public void ProtocolStringsAndMessageSizesAreBounded()
        {
            Assert.Throws<ArgumentException>(() => MultiplayerProtocol.Encode(
                ProtocolMessage.Hello(new string('a', 65), "build")));

            Assert.That(MultiplayerProtocol.IsValidEncodedSize(0), Is.False);
            Assert.That(MultiplayerProtocol.IsValidEncodedSize(MultiplayerProtocol.HeaderSize - 1), Is.False);
            Assert.That(MultiplayerProtocol.IsValidEncodedSize(MultiplayerProtocol.HeaderSize), Is.True);
            Assert.That(MultiplayerProtocol.IsValidEncodedSize(MultiplayerProtocol.MaximumMessageSize), Is.True);
            Assert.That(MultiplayerProtocol.IsValidEncodedSize(MultiplayerProtocol.MaximumMessageSize + 1), Is.False);

            Assert.That(
                MultiplayerProtocol.TryDecode(
                    new byte[MultiplayerProtocol.MaximumMessageSize + 1],
                    out _),
                Is.EqualTo(ProtocolDecodeResult.InvalidSize));
        }

        [Test]
        public void ReplicasValidateDigestAndConverge()
        {
            ulong digest = AuthoritativeWorldSnapshot.CalculateDigest(
                1,
                "resource-a",
                ResourceAvailability.Harvested,
                1,
                1);
            ProtocolMessage snapshot = ProtocolMessage.Snapshot(
                1,
                "resource-a",
                ResourceAvailability.Harvested,
                1,
                1,
                digest);
            var first = new ReplicatedWorldState();
            var second = new ReplicatedWorldState();
            first.Apply(snapshot);
            second.Apply(snapshot);

            Assert.That(second.Digest, Is.EqualTo(first.Digest));
            Assert.That(second.Availability, Is.EqualTo(first.Availability));
            Assert.Throws<InvalidOperationException>(() => new ReplicatedWorldState().Apply(
                ProtocolMessage.Snapshot(1, "resource-a", ResourceAvailability.Harvested, 1, 1, digest + 1)));
        }

        [Test]
        public void ReservedHeaderAndControlStringsAreRefused()
        {
            byte[] reserved = MultiplayerProtocol.Encode(ProtocolMessage.Ready());
            reserved[7] = 1;
            Assert.That(
                MultiplayerProtocol.TryDecode(reserved, out _),
                Is.EqualTo(ProtocolDecodeResult.InvalidPayload));

            byte[] control = MultiplayerProtocol.Encode(ProtocolMessage.Hello("client-a", "build-a"));
            control[12] = 1;
            Assert.That(
                MultiplayerProtocol.TryDecode(control, out _),
                Is.EqualTo(ProtocolDecodeResult.InvalidPayload));
        }

        [Test]
        public void DigestCoversRevisionAndReplicatedCounters()
        {
            ulong baseline = AuthoritativeWorldSnapshot.CalculateDigest(
                1,
                "resource-a",
                ResourceAvailability.Harvested,
                1,
                1);

            Assert.That(AuthoritativeWorldSnapshot.CalculateDigest(
                2, "resource-a", ResourceAvailability.Harvested, 1, 1), Is.Not.EqualTo(baseline));
            Assert.That(AuthoritativeWorldSnapshot.CalculateDigest(
                1, "resource-a", ResourceAvailability.Harvested, 2, 1), Is.Not.EqualTo(baseline));
            Assert.That(AuthoritativeWorldSnapshot.CalculateDigest(
                1, "resource-a", ResourceAvailability.Harvested, 1, 2), Is.Not.EqualTo(baseline));
        }

        [Test]
        public void SameRevisionCannotBeRewrittenWithDifferentState()
        {
            var replica = new ReplicatedWorldState();
            ulong availableDigest = AuthoritativeWorldSnapshot.CalculateDigest(
                1, "resource-a", ResourceAvailability.Available, 0, 0);
            replica.Apply(ProtocolMessage.Snapshot(
                1, "resource-a", ResourceAvailability.Available, 0, 0, availableDigest));

            ulong harvestedDigest = AuthoritativeWorldSnapshot.CalculateDigest(
                1, "resource-a", ResourceAvailability.Harvested, 1, 1);
            Assert.Throws<InvalidOperationException>(() => replica.Apply(ProtocolMessage.Snapshot(
                1, "resource-a", ResourceAvailability.Harvested, 1, 1, harvestedDigest)));
        }
    }
}
