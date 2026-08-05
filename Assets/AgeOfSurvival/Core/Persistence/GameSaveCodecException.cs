using System;

namespace AgeOfSurvival.Core.Persistence
{
    public enum GameSaveCodecViolation
    {
        InputMissing = 0,
        InputTooSmall = 1,
        InvalidMagic = 2,
        UnsupportedVersion = 3,
        UnknownFlags = 4,
        PayloadTooLarge = 5,
        LengthMismatch = 6,
        IntegrityMismatch = 7,
        UnexpectedEnd = 8,
        InvalidUtf8 = 9,
        InvalidStringLength = 10,
        CountLimitExceeded = 11,
        UnknownEnumValue = 12,
        InvalidBoolean = 13,
        NonCanonicalOrder = 14,
        DuplicateIdentity = 15,
        TrailingPayloadBytes = 16,
        InvalidDomainValue = 17
    }

    public sealed class GameSaveCodecException : FormatException
    {
        public GameSaveCodecException(
            GameSaveCodecViolation violation,
            string message,
            Exception innerException = null)
            : base(message, innerException)
        {
            Violation = violation;
        }

        public GameSaveCodecViolation Violation { get; }
    }
}
