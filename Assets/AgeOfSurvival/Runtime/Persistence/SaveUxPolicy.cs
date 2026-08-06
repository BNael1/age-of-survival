using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Runtime.Persistence
{
    public readonly struct SaveSlotId : IEquatable<SaveSlotId>, IComparable<SaveSlotId>
    {
        public SaveSlotId(int index)
        {
            if (index < 1 || index > SaveSlotPolicy.SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            Index = index;
        }

        public int Index { get; }
        public string StorageKey => $"slot-{Index}";
        public string DisplayName => $"Partie {Index}";

        public int CompareTo(SaveSlotId other) => Index.CompareTo(other.Index);
        public bool Equals(SaveSlotId other) => Index == other.Index;
        public override bool Equals(object obj) => obj is SaveSlotId other && Equals(other);
        public override int GetHashCode() => Index;
        public override string ToString() => StorageKey;
    }

    public static class SaveSlotPolicy
    {
        public const int SlotCount = 3;
        public const double AutosaveIntervalSeconds = 600d;

        private static readonly SaveSlotId[] Slots =
        {
            new SaveSlotId(1),
            new SaveSlotId(2),
            new SaveSlotId(3)
        };

        public static IReadOnlyList<SaveSlotId> All => Array.AsReadOnly(Slots);
    }

    public readonly struct SaveSlotMetadata
    {
        public SaveSlotMetadata(
            SaveSlotId slot,
            DateTime savedAtUtc,
            double playedSeconds,
            WorldSeed seed,
            bool recoveredFromBackup)
        {
            if (savedAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("The save timestamp must be UTC.", nameof(savedAtUtc));
            }

            if (double.IsNaN(playedSeconds)
                || double.IsInfinity(playedSeconds)
                || playedSeconds < 0d
                || playedSeconds > TimeSpan.MaxValue.TotalSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(playedSeconds));
            }

            Slot = slot;
            SavedAtUtc = savedAtUtc;
            PlayedSeconds = playedSeconds;
            Seed = seed;
            RecoveredFromBackup = recoveredFromBackup;
        }

        public SaveSlotId Slot { get; }
        public DateTime SavedAtUtc { get; }
        public double PlayedSeconds { get; }
        public WorldSeed Seed { get; }
        public bool RecoveredFromBackup { get; }
    }

    public readonly struct SaveSlotView
    {
        public SaveSlotView(
            SaveSlotId slot,
            bool exists,
            SaveSlotMetadata metadata,
            string error)
        {
            Slot = slot;
            Exists = exists;
            Metadata = metadata;
            Error = error ?? string.Empty;
        }

        public SaveSlotId Slot { get; }
        public bool Exists { get; }
        public SaveSlotMetadata Metadata { get; }
        public string Error { get; }
        public bool HasMetadata => Metadata.Slot.Equals(Slot);
        public bool IsReadable => Exists;

        public string Describe()
        {
            if (!Exists) return $"{Slot.DisplayName} — vide";
            if (!HasMetadata || !string.IsNullOrEmpty(Error))
            {
                return $"{Slot.DisplayName} — sauvegarde présente — informations indisponibles";
            }

            TimeSpan played = TimeSpan.FromSeconds(Metadata.PlayedSeconds);
            string recovery = Metadata.RecoveredFromBackup
                ? " — backup récupéré"
                : string.Empty;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} — {1:yyyy-MM-dd HH:mm} UTC — {2:00}:{3:00} — seed {4}{5}",
                Slot.DisplayName,
                Metadata.SavedAtUtc,
                (int)played.TotalHours,
                played.Minutes,
                Metadata.Seed,
                recovery);
        }
    }

    public enum SaveRequestKind
    {
        Manual = 0,
        Autosave = 1,
        ReturnToMainMenu = 2,
        Quit = 3
    }

    public sealed class SaveRequestScheduler
    {
        private SaveRequestKind? _pending;
        private double _elapsedSinceSave;

        public bool HasPending => _pending.HasValue;
        public SaveRequestKind PendingKind => _pending ?? SaveRequestKind.Autosave;
        public double ElapsedSinceSave => _elapsedSinceSave;

        public void Advance(double unscaledSeconds)
        {
            if (double.IsNaN(unscaledSeconds)
                || double.IsInfinity(unscaledSeconds)
                || unscaledSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledSeconds));
            }

            _elapsedSinceSave += unscaledSeconds;
            if (_elapsedSinceSave >= SaveSlotPolicy.AutosaveIntervalSeconds)
            {
                Request(SaveRequestKind.Autosave);
            }
        }

        public void Request(SaveRequestKind kind)
        {
            if (!Enum.IsDefined(typeof(SaveRequestKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (!_pending.HasValue || Priority(kind) > Priority(_pending.Value))
            {
                _pending = kind;
            }
        }

        public bool TryConsume(bool isSafePoint, out SaveRequestKind kind)
        {
            if (!isSafePoint || !_pending.HasValue)
            {
                kind = default;
                return false;
            }

            kind = _pending.Value;
            _pending = null;
            return true;
        }

        public void MarkSaved()
        {
            _elapsedSinceSave = 0d;
        }

        private static int Priority(SaveRequestKind kind)
        {
            switch (kind)
            {
                case SaveRequestKind.Quit:
                    return 4;
                case SaveRequestKind.ReturnToMainMenu:
                    return 3;
                case SaveRequestKind.Manual:
                    return 2;
                default:
                    return 1;
            }
        }
    }

    public sealed class SaveSlotMetadataStore
    {
        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(false, true);
        private readonly string _rootDirectory;

        public SaveSlotMetadataStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("A metadata root is required.", nameof(rootDirectory));
            }

            _rootDirectory = Path.GetFullPath(rootDirectory);
        }

        public void Write(SaveSlotMetadata metadata)
        {
            Directory.CreateDirectory(_rootDirectory);
            string target = GetPath(metadata.Slot);
            string temporary = target + ".tmp";
            string contents = string.Join("\n", new[]
            {
                "version=1",
                $"slot={metadata.Slot.Index}",
                $"savedUtcTicks={metadata.SavedAtUtc.Ticks}",
                $"playedSeconds={metadata.PlayedSeconds.ToString("R", CultureInfo.InvariantCulture)}",
                $"seed={metadata.Seed.Value}",
                $"backup={(metadata.RecoveredFromBackup ? 1 : 0)}",
                string.Empty
            });

            try
            {
                File.WriteAllText(temporary, contents, StrictUtf8);
                if (File.Exists(target))
                {
                    File.Replace(temporary, target, target + ".bak", true);
                }
                else
                {
                    File.Move(temporary, target);
                }
            }
            catch
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                throw;
            }
        }

        public bool TryRead(SaveSlotId slot, out SaveSlotMetadata metadata)
        {
            string path = GetPath(slot);
            if (!File.Exists(path))
            {
                metadata = default;
                return false;
            }

            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] lines = File.ReadAllLines(path, StrictUtf8);
            for (int index = 0; index < lines.Length; index++)
            {
                int separator = lines[index].IndexOf('=');
                if (separator <= 0) continue;
                fields.Add(
                    lines[index].Substring(0, separator),
                    lines[index].Substring(separator + 1));
            }

            if (!fields.TryGetValue("version", out string version)
                || version != "1"
                || !TryInt(fields, "slot", out int slotIndex)
                || slotIndex != slot.Index
                || !TryLong(fields, "savedUtcTicks", out long ticks)
                || !TryDouble(fields, "playedSeconds", out double playedSeconds)
                || !TryUlong(fields, "seed", out ulong seed)
                || !TryInt(fields, "backup", out int backup)
                || (backup != 0 && backup != 1))
            {
                throw new InvalidDataException("The slot metadata is invalid.");
            }

            metadata = new SaveSlotMetadata(
                slot,
                new DateTime(ticks, DateTimeKind.Utc),
                playedSeconds,
                new WorldSeed(seed),
                backup == 1);
            return true;
        }

        private string GetPath(SaveSlotId slot) =>
            Path.Combine(_rootDirectory, slot.StorageKey + ".aosmeta");

        private static bool TryInt(
            IReadOnlyDictionary<string, string> fields,
            string key,
            out int value)
        {
            value = default;
            return fields.TryGetValue(key, out string text)
                && int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value);
        }

        private static bool TryLong(
            IReadOnlyDictionary<string, string> fields,
            string key,
            out long value)
        {
            value = default;
            return fields.TryGetValue(key, out string text)
                && long.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value);
        }

        private static bool TryUlong(
            IReadOnlyDictionary<string, string> fields,
            string key,
            out ulong value)
        {
            value = default;
            return fields.TryGetValue(key, out string text)
                && ulong.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value);
        }

        private static bool TryDouble(
            IReadOnlyDictionary<string, string> fields,
            string key,
            out double value)
        {
            value = default;
            return fields.TryGetValue(key, out string text)
                && double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value)
                && !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value >= 0d;
        }
    }
}
