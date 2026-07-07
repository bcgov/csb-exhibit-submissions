using System.Globalization;
using System.Text;
using CES.Business.Constants;

namespace CES.Business.FileStorage
{
    // Turns (locationId, roomCode, shortDate, submissionId, exhibitId, ext) into a
    // safe relative path guaranteed to stay under AcceptedPath. Security-critical:
    // this is the injection surface for the accepted store (CES-39, Phase 2).
    public static class AcceptedPathBuilder
    {
        // Deterministic & idempotent segment sanitizer.
        // Whitelist charset = lowercased alphanumerics + '-'. Everything else
        // (path separators, '.', '..', absolute-path markers, other punctuation)
        // is stripped. Lowercased because the target FS/object-store may be
        // case-sensitive, so we normalize for a stable, collision-free key.
        public static string SanitizeSegment(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new PathTraversalException("Path segment cannot be empty.");

            var sb = new StringBuilder(raw.Length);
            foreach (var ch in raw)
            {
                if (ch >= 'A' && ch <= 'Z')
                    sb.Append(char.ToLowerInvariant(ch));
                else if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-')
                    sb.Append(ch);
                // all other characters (including '.', '/', '\\', whitespace) are dropped
            }

            var sanitized = sb.ToString();

            if (sanitized.Length == 0)
                throw new PathTraversalException("Path segment contains no allowed characters.");

            if (sanitized.Length > AcceptedStorageConstants.MaxSegmentLength)
                throw new PathTraversalException($"Path segment exceeds {AcceptedStorageConstants.MaxSegmentLength} characters.");

            return sanitized;
        }

        // Builds {loc}/{room}/{date}/{submissionId}/{exhibitId}{ext} using sanitized
        // segments. submissionId is a system-generated int (range-checked defensively)
        // and exhibitId is a GUID, so neither needs charset sanitization.
        public static string BuildCanonicalRelativePath(
            string locationId,
            string roomCode,
            string shortDate,
            int submissionId,
            Guid exhibitId,
            string? extension)
        {
            if (submissionId <= 0)
                throw new PathTraversalException("Submission id must be a positive integer.");

            if (exhibitId == Guid.Empty)
                throw new PathTraversalException("Exhibit id must be a non-empty GUID.");

            var loc = SanitizeSegment(locationId);
            var room = SanitizeSegment(roomCode);
            var date = SanitizeSegment(shortDate);
            var ext = NormalizeExtension(extension);
            var leaf = $"{exhibitId}{ext}";

            return string.Join('/', loc, room, date, submissionId.ToString(CultureInfo.InvariantCulture), leaf);
        }

        // The {exhibitId}{ext} leaf file name for a given exhibit.
        public static string BuildAcceptedFileName(Guid exhibitId, string? extension)
        {
            if (exhibitId == Guid.Empty)
                throw new PathTraversalException("Exhibit id must be a non-empty GUID.");

            return $"{exhibitId}{NormalizeExtension(extension)}";
        }

        // Combines acceptedRoot + relativePath, resolves to a full path, and asserts
        // it stays under the root. Throws PathTraversalException on any escape.
        public static string ResolveAndVerifyWithinRoot(string acceptedRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(acceptedRoot))
                throw new PathTraversalException("Accepted root path is not configured.");
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new PathTraversalException("Relative path cannot be empty.");

            var fullRoot = Path.GetFullPath(acceptedRoot);
            var rootWithSep = fullRoot.EndsWith(Path.DirectorySeparatorChar)
                ? fullRoot
                : fullRoot + Path.DirectorySeparatorChar;

            var combined = Path.GetFullPath(Path.Combine(fullRoot, relativePath));

            // The combined path must be strictly under the root (with the trailing
            // separator guard so "/root-evil" cannot masquerade as "/root").
            if (!combined.StartsWith(rootWithSep, StringComparison.Ordinal))
                throw new PathTraversalException($"Resolved path '{relativePath}' escapes the accepted store root.");

            return combined;
        }

        private static string NormalizeExtension(string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return string.Empty;

            var ext = extension.Trim();
            if (!ext.StartsWith('.'))
                ext = "." + ext;

            // Keep only a dot followed by lowercased alphanumerics — no separators,
            // no traversal, no double-dots.
            var sb = new StringBuilder(ext.Length);
            sb.Append('.');
            foreach (var ch in ext.AsSpan(1))
            {
                if (ch >= 'A' && ch <= 'Z')
                    sb.Append(char.ToLowerInvariant(ch));
                else if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                    sb.Append(ch);
            }

            return sb.Length == 1 ? string.Empty : sb.ToString();
        }
    }
}
