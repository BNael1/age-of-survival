using System;
using System.IO;
using System.Text;

namespace AgeOfSurvival.Core.Persistence
{
    internal sealed class SaveBufferWriter : IDisposable
    {
        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(false, true);

        private readonly MemoryStream _stream = new MemoryStream();
        private readonly byte[] _scratch = new byte[8];
        private readonly int _maximumLength;

        public SaveBufferWriter(int maximumLength = int.MaxValue)
        {
            if (maximumLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumLength));
            }

            _maximumLength = maximumLength;
        }

        public int Length => checked((int)_stream.Length);

        public void WriteByte(byte value)
        {
            EnsureWritable(1);
            _stream.WriteByte(value);
        }

        public void WriteBoolean(bool value)
        {
            WriteByte(value ? (byte)1 : (byte)0);
        }

        public void WriteUInt16(ushort value)
        {
            EnsureWritable(2);
            _scratch[0] = (byte)value;
            _scratch[1] = (byte)(value >> 8);
            _stream.Write(_scratch, 0, 2);
        }

        public void WriteInt32(int value)
        {
            WriteUInt32(unchecked((uint)value));
        }

        public void WriteUInt32(uint value)
        {
            EnsureWritable(4);
            _scratch[0] = (byte)value;
            _scratch[1] = (byte)(value >> 8);
            _scratch[2] = (byte)(value >> 16);
            _scratch[3] = (byte)(value >> 24);
            _stream.Write(_scratch, 0, 4);
        }

        public void WriteInt64(long value)
        {
            WriteUInt64(unchecked((ulong)value));
        }

        public void WriteUInt64(ulong value)
        {
            EnsureWritable(8);
            for (int index = 0; index < 8; index++)
            {
                _scratch[index] = (byte)(value >> (index * 8));
            }

            _stream.Write(_scratch, 0, 8);
        }

        public void WriteDouble(double value)
        {
            WriteInt64(BitConverter.DoubleToInt64Bits(
                value == 0d ? 0d : value));
        }

        public void WriteRequiredString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw Violation(
                    GameSaveCodecViolation.InvalidStringLength,
                    "A required save string must not be empty.");
            }

            WriteStringCore(value);
        }

        public void WriteOptionalString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                WriteUInt32(0);
                return;
            }

            WriteStringCore(value);
        }

        public void WriteBytes(byte[] value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            EnsureWritable(value.Length);
            _stream.Write(value, 0, value.Length);
        }

        public byte[] ToArray()
        {
            return _stream.ToArray();
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        private void EnsureWritable(int count)
        {
            if (count < 0 || _stream.Length + count > _maximumLength)
            {
                throw Violation(
                    GameSaveCodecViolation.PayloadTooLarge,
                    "The save buffer exceeds its configured limit.");
            }
        }

        private void WriteStringCore(string value)
        {
            byte[] bytes;
            try
            {
                bytes = StrictUtf8.GetBytes(value);
            }
            catch (EncoderFallbackException exception)
            {
                throw Violation(
                    GameSaveCodecViolation.InvalidUtf8,
                    "A save string cannot be encoded as strict UTF-8.",
                    exception);
            }

            if (bytes.Length > GameSaveCodecLimits.MaximumStringByteLength)
            {
                throw Violation(
                    GameSaveCodecViolation.InvalidStringLength,
                    "A save string exceeds the V1 byte limit.");
            }

            WriteUInt32(checked((uint)bytes.Length));
            WriteBytes(bytes);
        }

        private static GameSaveCodecException Violation(
            GameSaveCodecViolation violation,
            string message,
            Exception innerException = null)
        {
            return new GameSaveCodecException(
                violation,
                message,
                innerException);
        }
    }

    internal sealed class SaveBufferReader
    {
        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(false, true);

        private readonly byte[] _data;
        private int _offset;

        public SaveBufferReader(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public int Remaining => _data.Length - _offset;

        public byte ReadByte()
        {
            Require(1);
            return _data[_offset++];
        }

        public bool ReadBoolean()
        {
            byte value = ReadByte();
            if (value > 1)
            {
                throw Violation(
                    GameSaveCodecViolation.InvalidBoolean,
                    "A save boolean must be encoded as zero or one.");
            }

            return value == 1;
        }

        public ushort ReadUInt16()
        {
            Require(2);
            ushort value = (ushort)(
                _data[_offset]
                | (_data[_offset + 1] << 8));
            _offset += 2;
            return value;
        }

        public int ReadInt32()
        {
            return unchecked((int)ReadUInt32());
        }

        public uint ReadUInt32()
        {
            Require(4);
            uint value =
                _data[_offset]
                | ((uint)_data[_offset + 1] << 8)
                | ((uint)_data[_offset + 2] << 16)
                | ((uint)_data[_offset + 3] << 24);
            _offset += 4;
            return value;
        }

        public long ReadInt64()
        {
            return unchecked((long)ReadUInt64());
        }

        public ulong ReadUInt64()
        {
            Require(8);
            ulong value = 0;
            for (int index = 0; index < 8; index++)
            {
                value |= (ulong)_data[_offset + index] << (index * 8);
            }

            _offset += 8;
            return value;
        }

        public double ReadDouble()
        {
            double value = BitConverter.Int64BitsToDouble(ReadInt64());
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw Violation(
                    GameSaveCodecViolation.InvalidDomainValue,
                    "A saved coordinate must be finite.");
            }

            return value == 0d ? 0d : value;
        }

        public string ReadRequiredString()
        {
            string value = ReadStringCore();
            if (value.Length == 0)
            {
                throw Violation(
                    GameSaveCodecViolation.InvalidStringLength,
                    "A required save string is empty.");
            }

            return value;
        }

        public string ReadOptionalString()
        {
            return ReadStringCore();
        }

        public int ReadCount(int maximum, string label)
        {
            uint raw = ReadUInt32();
            if (raw > maximum)
            {
                throw Violation(
                    GameSaveCodecViolation.CountLimitExceeded,
                    $"{label} exceeds the V1 limit.");
            }

            return checked((int)raw);
        }

        public byte[] ReadBytes(int length)
        {
            Require(length);
            var result = new byte[length];
            Buffer.BlockCopy(_data, _offset, result, 0, length);
            _offset += length;
            return result;
        }

        public void RequireEnd()
        {
            if (Remaining != 0)
            {
                throw Violation(
                    GameSaveCodecViolation.TrailingPayloadBytes,
                    "The payload contains trailing bytes.");
            }
        }

        private string ReadStringCore()
        {
            uint rawLength = ReadUInt32();
            if (rawLength > GameSaveCodecLimits.MaximumStringByteLength)
            {
                throw Violation(
                    GameSaveCodecViolation.InvalidStringLength,
                    "A save string exceeds the V1 byte limit.");
            }

            int length = checked((int)rawLength);
            Require(length);
            try
            {
                string value = StrictUtf8.GetString(_data, _offset, length);
                _offset += length;
                return value;
            }
            catch (DecoderFallbackException exception)
            {
                throw Violation(
                    GameSaveCodecViolation.InvalidUtf8,
                    "A save string is not valid UTF-8.",
                    exception);
            }
        }

        private void Require(int length)
        {
            if (length < 0 || length > Remaining)
            {
                throw Violation(
                    GameSaveCodecViolation.UnexpectedEnd,
                    "The save payload ended unexpectedly.");
            }
        }

        private static GameSaveCodecException Violation(
            GameSaveCodecViolation violation,
            string message,
            Exception innerException = null)
        {
            return new GameSaveCodecException(
                violation,
                message,
                innerException);
        }
    }
}
