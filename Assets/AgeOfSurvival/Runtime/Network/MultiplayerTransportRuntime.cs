using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Resources;
using AgeOfSurvival.Core.Simulation;
using AgeOfSurvival.Core.World.Generation;
using AgeOfSurvival.Protocol;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Network
{
    public static class MultiplayerBuildInfo
    {
        public const string Version = "7E-B.1";
    }

    internal static class MultiplayerRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartFromCommandLine()
        {
            string role = CommandLine.Value("aos-role");
            if (role != "server" && role != "client-smoke") return;

            var root = new GameObject("Age of Survival Network Process Adapter");
            UnityEngine.Object.DontDestroyOnLoad(root);
            MultiplayerProcessSession.Start(role);
            root.AddComponent<MultiplayerProcessAdapter>();
        }
    }

    internal sealed class MultiplayerProcessAdapter : MonoBehaviour
    {
        private void Update()
        {
            MultiplayerProcessSession.Tick();
        }

        private void OnDestroy()
        {
            MultiplayerProcessSession.Stop();
        }
    }

    internal static class MultiplayerProcessSession
    {
        private static IProcessRole s_role;

        public static void Start(string role)
        {
            if (s_role != null) throw new InvalidOperationException("A network process session is already active.");
            try
            {
                s_role = role == "server"
                    ? (IProcessRole)new ServerProcessRole()
                    : new ClientSmokeProcessRole();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[AOS-NET] startup_failed error={exception.GetType().Name} message={exception.Message}");
                Application.Quit(2);
            }
        }

        public static void Tick()
        {
            try
            {
                s_role?.Tick();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[AOS-NET] fatal error={exception.GetType().Name} message={exception.Message}");
                Application.Quit(2);
            }
        }

        public static void Stop()
        {
            s_role?.Dispose();
            s_role = null;
        }
    }

    internal interface IProcessRole : IDisposable
    {
        void Tick();
    }

    internal sealed class ServerProcessRole : IProcessRole
    {
        private readonly NetworkDriver _driver;
        private readonly NetworkPipeline _pipeline;
        private readonly List<ServerPeer> _peers = new List<ServerPeer>();
        private readonly HashSet<string> _seenClientIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _completedClientIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly AuthoritativeMultiplayerSimulation _simulation;
        private readonly float _deadline;
        private bool _scenarioStarted;
        private bool _sawReconnect;
        private bool _disposed;

        public ServerProcessRole()
        {
            ushort port = CommandLine.Port("aos-port", 7777);
            WorldSeed seed = WorldSeed.Parse(CommandLine.Value("aos-seed") ?? "0");
            float duration = CommandLine.PositiveFloat("aos-duration-seconds", 45f);
            _deadline = Time.realtimeSinceStartup + duration;
            _simulation = new AuthoritativeMultiplayerSimulation(seed);

            var settings = new NetworkSettings();
            settings.WithNetworkConfigParameters(
                connectTimeoutMS: 1000,
                maxConnectAttempts: 10,
                disconnectTimeoutMS: 5000,
                heartbeatTimeoutMS: 500,
                maxMessageSize: MultiplayerProtocol.MaximumMessageSize);
            _driver = NetworkDriver.Create(settings);
            _pipeline = _driver.CreatePipeline(
                typeof(FragmentationPipelineStage),
                typeof(ReliableSequencedPipelineStage));
            NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4.WithPort(port);
            if (_driver.Bind(endpoint) != 0 || _driver.Listen() != 0)
            {
                _driver.Dispose();
                throw new InvalidOperationException($"Cannot listen on UDP port {port}.");
            }

            Debug.Log(
                $"[AOS-NET] server_started port={port} seed={seed} protocol={MultiplayerProtocol.Version} "
                + $"build={MultiplayerBuildInfo.Version} transport=UnityTransport-2.7.4");
        }

        public void Tick()
        {
            if (_disposed) return;
            _driver.ScheduleUpdate().Complete();
            NetworkConnection accepted;
            while ((accepted = _driver.Accept()) != default)
            {
                _peers.Add(new ServerPeer(accepted));
            }

            for (int peerIndex = _peers.Count - 1; peerIndex >= 0; peerIndex--)
            {
                ServerPeer peer = _peers[peerIndex];
                NetworkEvent.Type eventType;
                while ((eventType = _driver.PopEventForConnection(peer.Connection, out DataStreamReader reader))
                    != NetworkEvent.Type.Empty)
                {
                    if (eventType == NetworkEvent.Type.Data)
                    {
                        if (!MultiplayerProtocol.IsValidEncodedSize(reader.Length))
                        {
                            RejectPeer(peer, "protocol_InvalidSize");
                            break;
                        }

                        Handle(peer, TransportBytes.Read(reader));
                    }
                    else if (eventType == NetworkEvent.Type.Disconnect)
                    {
                        DisconnectPeer(peer, "remote_disconnect");
                        _peers.RemoveAt(peerIndex);
                        break;
                    }
                }
            }

            if (!_scenarioStarted && ReadyPeerCount() >= 2)
            {
                _scenarioStarted = true;
                ProtocolMessage start = ProtocolMessage.ScenarioStart();
                for (int index = 0; index < _peers.Count; index++)
                {
                    if (_peers[index].Ready && _peers[index].ClientId != null)
                    {
                        Send(_peers[index], start);
                    }
                }
                Debug.Log($"[AOS-NET] scenario_started clients={ReadyPeerCount()} digest={_simulation.CreateSnapshot().Digest:X16}");
            }

            if (_completedClientIds.Count >= 2 && _sawReconnect)
            {
                AuthoritativeWorldSnapshot final = _simulation.CreateSnapshot();
                if (final.Revision > 0
                    && final.Availability == ResourceAvailability.Harvested
                    && final.EvictionCount > 0
                    && final.RestorationCount > 0)
                {
                    Debug.Log(
                        $"[AOS-NET] server_smoke_pass clients=2 digest={final.Digest:X16} "
                        + $"evictions={final.EvictionCount} restorations={final.RestorationCount}");
                    Application.Quit(0);
                    return;
                }
            }

            if (Time.realtimeSinceStartup >= _deadline)
            {
                Debug.LogError("[AOS-NET] server_smoke_timeout");
                Application.Quit(3);
            }
        }

        private void Handle(ServerPeer peer, byte[] bytes)
        {
            ProtocolDecodeResult decoded = MultiplayerProtocol.TryDecode(bytes, out ProtocolMessage message);
            if (decoded != ProtocolDecodeResult.Success)
            {
                RejectPeer(peer, $"protocol_{decoded}");
                return;
            }

            switch (message.Type)
            {
                case ProtocolMessageType.Hello:
                    HandleHello(peer, message);
                    break;
                case ProtocolMessageType.Ready:
                    if (!EnsureAuthenticated(peer)) return;
                    peer.Ready = true;
                    break;
                case ProtocolMessageType.HarvestIntent:
                    HandleHarvest(peer, message);
                    break;
                case ProtocolMessageType.ClientComplete:
                    if (!EnsureAuthenticated(peer)) return;
                    if (!_scenarioStarted || !peer.Ready)
                    {
                        RejectPeer(peer, "completion_before_scenario");
                        return;
                    }

                    if (message.Digest != _simulation.CreateSnapshot().Digest)
                    {
                        RejectPeer(peer, "divergent_completion_digest");
                        return;
                    }

                    _completedClientIds.Add(peer.ClientId);
                    break;
                default:
                    RejectPeer(peer, $"unexpected_{message.Type}");
                    break;
            }
        }

        private void HandleHello(ServerPeer peer, ProtocolMessage message)
        {
            if (peer.ClientId != null)
            {
                RejectPeer(peer, "duplicate_hello");
                return;
            }

            if (!string.Equals(
                    message.BuildVersion,
                    MultiplayerBuildInfo.Version,
                    StringComparison.Ordinal))
            {
                RejectPeer(peer, "incompatible_build");
                return;
            }

            ServerPeer previous = FindPeer(message.ClientId);
            if (previous != null)
            {
                RejectPeer(previous, "replaced_by_reconnect");
            }

            bool reconnect = !_seenClientIds.Add(message.ClientId);
            _sawReconnect |= reconnect;
            _simulation.Connect(message.ClientId);
            peer.ClientId = message.ClientId;
            if (!Send(peer, ProtocolMessage.Welcome(MultiplayerBuildInfo.Version))
                || !SendSnapshot(peer))
            {
                return;
            }

            Debug.Log(
                $"[AOS-NET] client_accepted id={message.ClientId} reconnect={reconnect} "
                + $"client_build={message.BuildVersion} protocol={MultiplayerProtocol.Version}");
        }

        private void HandleHarvest(ServerPeer peer, ProtocolMessage message)
        {
            if (!EnsureAuthenticated(peer)) return;
            if (!_scenarioStarted || !peer.Ready)
            {
                RejectPeer(peer, "harvest_before_scenario");
                return;
            }

            AuthoritativeCommandResult result = _simulation.Harvest(
                peer.ClientId,
                message.Sequence,
                new ResourceId(message.ResourceId));
            if (!result.Accepted)
            {
                Send(peer, ProtocolMessage.CommandRejected(message.Sequence, result.Rejection, result.Digest));
                Debug.Log(
                    $"[AOS-NET] command_rejected client={peer.ClientId} sequence={message.Sequence} "
                    + $"reason={result.Rejection} digest={result.Digest:X16}");
                return;
            }

            for (int index = 0; index < _peers.Count; index++)
            {
                if (_peers[index].ClientId != null) SendSnapshot(_peers[index]);
            }
            AuthoritativeWorldSnapshot snapshot = _simulation.CreateSnapshot();
            Debug.Log(
                $"[AOS-NET] mutation_applied client={peer.ClientId} revision={snapshot.Revision} "
                + $"digest={snapshot.Digest:X16} evictions={snapshot.EvictionCount} "
                + $"restorations={snapshot.RestorationCount}");
        }

        private bool SendSnapshot(ServerPeer peer)
        {
            AuthoritativeWorldSnapshot state = _simulation.CreateSnapshot();
            return Send(peer, ProtocolMessage.Snapshot(
                state.Revision,
                state.ResourceId.Value,
                state.Availability,
                state.EvictionCount,
                state.RestorationCount,
                state.Digest));
        }

        private bool Send(ServerPeer peer, ProtocolMessage message)
        {
            if (!peer.Connection.IsCreated) return false;
            byte[] encoded = MultiplayerProtocol.Encode(message);
            try
            {
                TransportBytes.Send(_driver, _pipeline, peer.Connection, encoded);
                return true;
            }
            catch (InvalidOperationException)
            {
                RejectPeer(peer, "transport_send_failed");
                return false;
            }
        }

        private bool EnsureAuthenticated(ServerPeer peer)
        {
            if (peer.ClientId != null) return true;
            RejectPeer(peer, "unauthenticated_message");
            return false;
        }

        private void RejectPeer(ServerPeer peer, string reason)
        {
            Debug.LogWarning($"[AOS-NET] peer_rejected reason={reason}");
            if (peer.Connection.IsCreated) peer.Connection.Disconnect(_driver);
            DisconnectPeer(peer, reason);
        }

        private int ReadyPeerCount()
        {
            int count = 0;
            for (int index = 0; index < _peers.Count; index++)
            {
                if (_peers[index].Ready && _peers[index].ClientId != null) count++;
            }

            return count;
        }

        private ServerPeer FindPeer(string clientId)
        {
            for (int index = 0; index < _peers.Count; index++)
            {
                if (string.Equals(_peers[index].ClientId, clientId, StringComparison.Ordinal)) return _peers[index];
            }

            return null;
        }

        private void DisconnectPeer(ServerPeer peer, string reason)
        {
            if (peer.ClientId == null) return;
            _simulation.Disconnect(peer.ClientId);
            Debug.Log($"[AOS-NET] client_disconnected id={peer.ClientId} reason={reason}");
            peer.ClientId = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int index = 0; index < _peers.Count; index++)
            {
                if (_peers[index].Connection.IsCreated) _peers[index].Connection.Disconnect(_driver);
            }

            _driver.ScheduleUpdate().Complete();
            _driver.Dispose();
            Debug.Log("[AOS-NET] server_stopped clean=true");
        }

        private sealed class ServerPeer
        {
            public ServerPeer(NetworkConnection connection) { Connection = connection; }
            public NetworkConnection Connection { get; }
            public string ClientId { get; set; }
            public bool Ready { get; set; }
        }
    }

    internal sealed class ClientSmokeProcessRole : IProcessRole
    {
        private readonly NetworkDriver _driver;
        private readonly NetworkPipeline _pipeline;
        private readonly NetworkEndpoint _endpoint;
        private readonly string _clientId;
        private readonly bool _harvester;
        private readonly ReplicatedWorldState _state = new ReplicatedWorldState();
        private readonly float _deadline;
        private NetworkConnection _connection;
        private bool _helloSent;
        private bool _readySent;
        private bool _invalidSent;
        private bool _reconnectPending;
        private bool _reconnected;
        private float _reconnectAt;
        private ulong _digestBeforeReconnect;
        private bool _disposed;

        public ClientSmokeProcessRole()
        {
            string host = CommandLine.Value("aos-host") ?? "127.0.0.1";
            ushort port = CommandLine.Port("aos-port", 7777);
            _clientId = CommandLine.Value("aos-client-id") ?? "smoke-client";
            StableIdentifierValidation.Validate(_clientId, nameof(_clientId));
            _harvester = string.Equals(CommandLine.Value("aos-action"), "harvest", StringComparison.Ordinal);
            _deadline = Time.realtimeSinceStartup + CommandLine.PositiveFloat("aos-timeout-seconds", 30f);

            if (!NetworkEndpoint.TryParse(host, port, out _endpoint, NetworkFamily.Ipv4))
            {
                throw new ArgumentException($"Invalid IPv4 server endpoint {host}:{port}.");
            }

            var settings = new NetworkSettings();
            settings.WithNetworkConfigParameters(
                connectTimeoutMS: 1000,
                maxConnectAttempts: 10,
                disconnectTimeoutMS: 5000,
                heartbeatTimeoutMS: 500,
                maxMessageSize: MultiplayerProtocol.MaximumMessageSize);
            _driver = NetworkDriver.Create(settings);
            _pipeline = _driver.CreatePipeline(
                typeof(FragmentationPipelineStage),
                typeof(ReliableSequencedPipelineStage));
            Connect();
        }

        public void Tick()
        {
            if (_disposed) return;
            _driver.ScheduleUpdate().Complete();
            if (_connection.IsCreated)
            {
                NetworkEvent.Type eventType;
                while ((eventType = _connection.PopEvent(_driver, out DataStreamReader reader))
                    != NetworkEvent.Type.Empty)
                {
                    if (eventType == NetworkEvent.Type.Connect)
                    {
                        Send(ProtocolMessage.Hello(_clientId, MultiplayerBuildInfo.Version));
                        _helloSent = true;
                    }
                    else if (eventType == NetworkEvent.Type.Data)
                    {
                        Handle(TransportBytes.Read(reader));
                    }
                    else if (eventType == NetworkEvent.Type.Disconnect)
                    {
                        _connection = default;
                        if (_reconnectPending) _reconnectAt = Time.realtimeSinceStartup + 0.5f;
                    }
                }
            }
            else if (_reconnectPending && Time.realtimeSinceStartup >= _reconnectAt)
            {
                _reconnectPending = false;
                _reconnected = true;
                _helloSent = false;
                _readySent = false;
                Connect();
            }

            if (Time.realtimeSinceStartup >= _deadline)
            {
                Debug.LogError($"[AOS-NET] client_smoke_timeout id={_clientId}");
                Application.Quit(4);
            }
        }

        private void Handle(byte[] bytes)
        {
            ProtocolDecodeResult result = MultiplayerProtocol.TryDecode(bytes, out ProtocolMessage message);
            if (result != ProtocolDecodeResult.Success)
            {
                throw new InvalidOperationException($"Protocol decode failed: {result}.");
            }

            switch (message.Type)
            {
                case ProtocolMessageType.Welcome:
                    Debug.Log(
                        $"[AOS-NET] client_welcome id={_clientId} protocol={MultiplayerProtocol.Version} "
                        + $"server_build={message.BuildVersion}");
                    break;
                case ProtocolMessageType.Snapshot:
                    _state.Apply(message);
                    Debug.Log(
                        $"[AOS-NET] client_snapshot id={_clientId} revision={_state.Revision} "
                        + $"digest={_state.Digest:X16} state={_state.Availability}");
                    if (_reconnected)
                    {
                        if (!_readySent)
                        {
                            Send(ProtocolMessage.Ready());
                            _readySent = true;
                        }

                        if (_state.Digest != _digestBeforeReconnect)
                        {
                            throw new InvalidOperationException("Reconnect snapshot diverged.");
                        }

                        Complete("reconnect_converged");
                    }
                    else if (!_readySent)
                    {
                        Send(ProtocolMessage.Ready());
                        _readySent = true;
                    }
                    else if (_state.Revision > 0 && _harvester && !_invalidSent)
                    {
                        Send(ProtocolMessage.HarvestIntent(2, _state.ResourceId));
                        _invalidSent = true;
                    }
                    else if (_state.Revision > 0 && !_harvester && !_reconnectPending)
                    {
                        _digestBeforeReconnect = _state.Digest;
                        _reconnectPending = true;
                        _connection.Disconnect(_driver);
                        _driver.ScheduleFlushSend().Complete();
                        _connection = default;
                        _reconnectAt = Time.realtimeSinceStartup + 0.5f;
                    }
                    break;
                case ProtocolMessageType.ScenarioStart:
                    if (_harvester) Send(ProtocolMessage.HarvestIntent(1, _state.ResourceId));
                    break;
                case ProtocolMessageType.CommandRejected:
                    if (!_harvester
                        || message.Rejection != AuthoritativeCommandRejection.AlreadyHarvested
                        || message.Digest != _state.Digest)
                    {
                        throw new InvalidOperationException("The invalid-command rejection changed state or reason.");
                    }

                    Complete("invalid_rejected");
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected server message {message.Type}.");
            }
        }

        private void Complete(string reason)
        {
            Send(ProtocolMessage.ClientComplete(_state.Digest));
            _driver.ScheduleFlushSend().Complete();
            Debug.Log(
                $"[AOS-NET] client_smoke_pass id={_clientId} reason={reason} digest={_state.Digest:X16} "
                + $"evictions={_state.EvictionCount} restorations={_state.RestorationCount}");
            Application.Quit(0);
        }

        private void Connect()
        {
            _connection = _driver.Connect(_endpoint);
            Debug.Log($"[AOS-NET] client_connecting id={_clientId} endpoint={_endpoint}");
        }

        private void Send(ProtocolMessage message)
        {
            if (!_connection.IsCreated || !_helloSent && message.Type != ProtocolMessageType.Hello)
            {
                throw new InvalidOperationException("Cannot send before the transport handshake.");
            }

            TransportBytes.Send(_driver, _pipeline, _connection, MultiplayerProtocol.Encode(message));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_connection.IsCreated) _connection.Disconnect(_driver);
            _driver.ScheduleUpdate().Complete();
            _driver.Dispose();
        }
    }

    internal static class TransportBytes
    {
        public static byte[] Read(DataStreamReader reader)
        {
            if (!MultiplayerProtocol.IsValidEncodedSize(reader.Length))
            {
                throw new InvalidOperationException("Transport payload size is invalid.");
            }

            var bytes = new byte[reader.Length];
            for (int index = 0; index < bytes.Length; index++) bytes[index] = reader.ReadByte();
            return bytes;
        }

        public static void Send(
            NetworkDriver driver,
            NetworkPipeline pipeline,
            NetworkConnection connection,
            byte[] bytes)
        {
            if (bytes == null || bytes.Length > MultiplayerProtocol.MaximumMessageSize)
            {
                throw new ArgumentException("Transport payload is invalid.", nameof(bytes));
            }

            int begin = driver.BeginSend(pipeline, connection, out DataStreamWriter writer);
            if (begin != 0) throw new InvalidOperationException($"Transport BeginSend failed with {begin}.");
            for (int index = 0; index < bytes.Length; index++) writer.WriteByte(bytes[index]);
            int sent = driver.EndSend(writer);
            if (sent < 0) throw new InvalidOperationException($"Transport EndSend failed with {sent}.");
        }
    }

    internal static class CommandLine
    {
        public static string Value(string name)
        {
            string prefix = $"--{name}=";
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (arguments[index].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return arguments[index].Substring(prefix.Length);
                }
            }

            return null;
        }

        public static ushort Port(string name, ushort fallback)
        {
            string value = Value(name);
            if (value == null) return fallback;
            if (!ushort.TryParse(value, out ushort port) || port == 0)
            {
                throw new ArgumentOutOfRangeException(name, "The UDP port must be between 1 and 65535.");
            }

            return port;
        }

        public static float PositiveFloat(string name, float fallback)
        {
            string value = Value(name);
            if (value == null) return fallback;
            if (!float.TryParse(
                    value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float parsed)
                || float.IsNaN(parsed)
                || float.IsInfinity(parsed)
                || parsed <= 0f)
            {
                throw new ArgumentOutOfRangeException(name, "A finite positive number is required.");
            }

            return parsed;
        }
    }
}
