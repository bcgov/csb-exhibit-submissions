using System.Text.RegularExpressions;
using CES.Business.Constants;

namespace CES.Business.Extensions
{
    /// <summary>
    /// The single definition of what a valid officer number is. Shared by the profile write
    /// (<c>PUT /api/users/me/officer-number</c>) and the submission create, so the two can never
    /// disagree about what the client is allowed to send.
    /// </summary>
    public static class OfficerNumberExtensions
    {
        private static readonly Regex AllowedCharacters =
            new(UserConstants.OfficerNumberPattern, RegexOptions.Compiled);

        /// <summary>
        /// Trims and validates an officer number, returning the value to persist.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when the value is missing, too long, or contains disallowed characters — all of
        /// which the <c>ApiExceptionMiddleware</c> maps to a <c>400</c> with this message.
        /// </exception>
        public static string NormalizeOfficerNumberOrThrow(this string? officerNumber)
        {
            var trimmed = officerNumber?.Trim();

            if (string.IsNullOrEmpty(trimmed))
                throw new ArgumentException("An officer number is required.");

            if (trimmed.Length > UserConstants.OfficerNumberMaxLength)
                throw new ArgumentException(
                    $"An officer number cannot exceed {UserConstants.OfficerNumberMaxLength} characters.");

            if (!AllowedCharacters.IsMatch(trimmed))
                throw new ArgumentException(
                    "An officer number may contain only letters, numbers, dashes and periods.");

            return trimmed;
        }
    }
}
