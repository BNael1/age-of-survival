using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Core.Shelter
{
    public interface IRoomShelterIdentityStrategy
    {
        ShelterId CreateId(IEnumerable<WorldCellCoordinate> cells);
    }

    /// <summary>PROTOTYPE / NON GAMEPLAY FINAL. No lineage or transfer between geometries.</summary>
    public sealed class RoomFingerprintIdentityStrategy : IRoomShelterIdentityStrategy
    {
        private readonly Func<byte[], byte[]> _hash;
        private readonly Dictionary<ShelterId, byte[]> _known = new Dictionary<ShelterId, byte[]>();
        public RoomFingerprintIdentityStrategy(Func<byte[], byte[]> hash = null) { _hash = hash ?? Hash; }
        public ShelterId CreateId(IEnumerable<WorldCellCoordinate> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            var canonical = new List<WorldCellCoordinate>(cells);
            if (canonical.Count == 0) throw new ArgumentException("A room cannot be empty.", nameof(cells));
            canonical.Sort();
            var bytes = new byte[checked(8 + canonical.Count * 16)];
            Write(bytes, 0, 1, 4); // stream schema version, UInt32 LE
            Write(bytes, 4, (ulong)canonical.Count, 4); // cell count, UInt32 LE
            for (int i = 0; i < canonical.Count; i++)
            {
                if (i > 0 && canonical[i].Equals(canonical[i - 1]))
                    throw new ArgumentException("Room cells must be unique.", nameof(cells));
                Write(bytes, 8 + i * 16, unchecked((ulong)canonical[i].X), 8);
                Write(bytes, 16 + i * 16, unchecked((ulong)canonical[i].Y), 8);
            }
            byte[] digest = _hash((byte[])bytes.Clone());
            if (digest == null || digest.Length != 32) throw new InvalidOperationException("Room hashing requires 256 bits.");
            var id = new ShelterId("room-v1-sha256:" + BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant());
            if (_known.TryGetValue(id, out byte[] previous))
            {
                if (previous.Length != bytes.Length) throw new InvalidOperationException("Room fingerprint collision.");
                for (int i = 0; i < bytes.Length; i++)
                    if (previous[i] != bytes[i]) throw new InvalidOperationException("Room fingerprint collision.");
            }
            else _known.Add(id, bytes);
            return id;
        }
        private static void Write(byte[] bytes, int offset, ulong value, int count)
        {
            for (int i = 0; i < count; i++) bytes[offset + i] = (byte)(value >> (8 * i));
        }
        private static byte[] Hash(byte[] bytes) { using (var sha = SHA256.Create()) return sha.ComputeHash(bytes); }
    }

    public interface IShelterRoomQualificationPolicy
    {
        ShelterInvalidReason Evaluate(ConstructionRoomCoverage room, ISet<WorldCellCoordinate> completedFloors);
    }

    /// <summary>PROTOTYPE / NON GAMEPLAY FINAL: enclosure + Floor everywhere + supported Roof everywhere.</summary>
    public sealed class FullFloorSupportedRoofShelterPolicy : IShelterRoomQualificationPolicy
    {
        public ShelterInvalidReason Evaluate(ConstructionRoomCoverage room, ISet<WorldCellCoordinate> completedFloors)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (completedFloors == null) throw new ArgumentNullException(nameof(completedFloors));
            foreach (WorldCellCoordinate cell in room.Room.Cells)
                if (!completedFloors.Contains(cell)) return ShelterInvalidReason.MissingConfiguredLocalSurface;
            return room.IsFullySupportedRoofed ? ShelterInvalidReason.None : ShelterInvalidReason.MissingSupportedRoof;
        }
    }

    public static class RoomShelterEvaluator
    {
        public static ShelterEvaluation Evaluate(IEnumerable<CompletedStructureState> structures,
            ConstructionDefinitionId floorId, ConstructionDerivedRooms rooms,
            IShelterRoomQualificationPolicy policy, IRoomShelterIdentityStrategy identity)
        {
            if (structures == null || rooms == null || policy == null || identity == null)
                throw new ArgumentNullException("Shelter evaluation dependencies cannot be null.");
            var floors = new HashSet<WorldCellCoordinate>();
            foreach (CompletedStructureState structure in structures)
                if (structure.DefinitionId.Equals(floorId) && structure.Space.Kind == ConstructionSpaceKind.Surface)
                    floors.Add(structure.Space.AnchorCell);
            var result = new List<ShelterAssessment>();
            foreach (ConstructionRoomCoverage room in rooms.Rooms)
                result.Add(new ShelterAssessment(identity.CreateId(room.Room.Cells), default, room,
                    policy.Evaluate(room, floors), 0));
            return new ShelterEvaluation(result);
        }
    }
}
