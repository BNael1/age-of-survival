using System;
using System.IO;

namespace AgeOfSurvival.Core.Persistence
{
    public enum GameSaveLoadSource
    {
        Primary = 0,
        Backup = 1
    }

    public readonly struct GameSaveLoadResult
    {
        public GameSaveLoadResult(
            GameSaveSnapshot snapshot,
            GameSaveLoadSource source)
        {
            Snapshot = snapshot
                ?? throw new ArgumentNullException(nameof(snapshot));
            if (!Enum.IsDefined(typeof(GameSaveLoadSource), source))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(source),
                    source,
                    "Unknown save source.");
            }

            Source = source;
        }

        public GameSaveSnapshot Snapshot { get; }
        public GameSaveLoadSource Source { get; }
    }

    /// <summary>
    /// File storage with a primary, previous primary backup and temporary write.
    /// Runtime selects the root directory; the Core owns no platform path.
    /// Calls are synchronous and require a single writer for each slot.
    /// </summary>
    public sealed class AtomicGameSaveStorage
    {
        private readonly string _rootDirectory;

        public AtomicGameSaveStorage(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException(
                    "A save root directory is required.",
                    nameof(rootDirectory));
            }

            _rootDirectory = Path.GetFullPath(rootDirectory);
        }

        public string RootDirectory => _rootDirectory;

        public void Save(string slot, GameSaveSnapshot snapshot)
        {
            ValidateSlot(slot);
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            byte[] bytes = GameSaveBinaryCodec.Encode(snapshot);
            Directory.CreateDirectory(_rootDirectory);

            string primary = GetPath(slot, ".aos");
            string backup = GetPath(slot, ".bak");
            string temporary = GetPath(slot, ".tmp");
            DeleteIfExists(temporary);

            try
            {
                using (var stream = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.SequentialScan))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(primary))
                {
                    File.Replace(
                        temporary,
                        primary,
                        backup,
                        ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(temporary, primary);
                }

                if (File.Exists(temporary))
                {
                    throw new IOException(
                        "The temporary save remained after promotion.");
                }
            }
            catch
            {
                DeleteIfExists(temporary);
                throw;
            }
        }

        public GameSaveLoadResult Load(string slot)
        {
            ValidateSlot(slot);
            string primary = GetPath(slot, ".aos");
            string backup = GetPath(slot, ".bak");

            Exception primaryFailure = null;
            if (File.Exists(primary))
            {
                try
                {
                    return new GameSaveLoadResult(
                        DecodeFile(primary),
                        GameSaveLoadSource.Primary);
                }
                catch (Exception exception)
                {
                    primaryFailure = exception;
                }
            }

            if (File.Exists(backup))
            {
                try
                {
                    return new GameSaveLoadResult(
                        DecodeFile(backup),
                        GameSaveLoadSource.Backup);
                }
                catch (Exception backupFailure)
                {
                    throw new AggregateException(
                        "Neither the primary save nor its backup is readable.",
                        primaryFailure ?? new FileNotFoundException(
                            "Primary save was not found.",
                            primary),
                        backupFailure);
                }
            }

            if (primaryFailure != null)
            {
                throw new InvalidDataException(
                    "The primary save is invalid and no backup exists.",
                    primaryFailure);
            }

            throw new FileNotFoundException(
                "No save exists for the requested slot.",
                primary);
        }

        public bool Exists(string slot)
        {
            ValidateSlot(slot);
            return File.Exists(GetPath(slot, ".aos"))
                || File.Exists(GetPath(slot, ".bak"));
        }

        public string GetPrimaryPath(string slot)
        {
            ValidateSlot(slot);
            return GetPath(slot, ".aos");
        }

        public string GetBackupPath(string slot)
        {
            ValidateSlot(slot);
            return GetPath(slot, ".bak");
        }

        public string GetTemporaryPath(string slot)
        {
            ValidateSlot(slot);
            return GetPath(slot, ".tmp");
        }

        private static GameSaveSnapshot DecodeFile(string path)
        {
            long maximum =
                GameSaveCodecLimits.HeaderLength
                + (long)GameSaveCodecLimits.MaximumPayloadLength;
            using (var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.SequentialScan))
            {
                if (stream.Length < GameSaveCodecLimits.HeaderLength
                    || stream.Length > maximum)
                {
                    throw new InvalidDataException(
                        "The save file size is outside the V1 bounds.");
                }

                int length = checked((int)stream.Length);
                var bytes = new byte[length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(
                        bytes,
                        offset,
                        bytes.Length - offset);
                    if (read == 0)
                    {
                        throw new EndOfStreamException(
                            "The save file ended while it was being read.");
                    }

                    offset += read;
                }

                if (stream.ReadByte() != -1)
                {
                    throw new InvalidDataException(
                        "The save file grew while it was being read.");
                }

                return GameSaveBinaryCodec.Decode(bytes);
            }
        }

        private string GetPath(string slot, string extension)
        {
            string path = Path.GetFullPath(
                Path.Combine(_rootDirectory, slot + extension));
            string rootWithSeparator = _rootDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!path.StartsWith(
                rootWithSeparator,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The save slot escapes the configured root.",
                    nameof(slot));
            }

            return path;
        }

        private static void ValidateSlot(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot)
                || slot.Length > 64
                || slot == "."
                || slot == "..")
            {
                throw new ArgumentException(
                    "A save slot must contain 1 to 64 safe characters.",
                    nameof(slot));
            }

            for (int index = 0; index < slot.Length; index++)
            {
                char value = slot[index];
                bool allowed =
                    (value >= 'a' && value <= 'z')
                    || (value >= 'A' && value <= 'Z')
                    || (value >= '0' && value <= '9')
                    || value == '-'
                    || value == '_';
                if (!allowed)
                {
                    throw new ArgumentException(
                        "A save slot contains an unsafe character.",
                        nameof(slot));
                }
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
