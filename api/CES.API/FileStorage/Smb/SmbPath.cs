using CES.Business.FileStorage;

namespace CES.API.FileStorage.Smb
{
    // Turns the store's OS-agnostic relative paths ("loc/room/date/123/file.ext") into
    // the share-relative, backslash-separated form SMB2's CreateFile expects, with
    // FileStorage:Smb:BasePath prepended.
    //
    // This is the SMB counterpart to AcceptedPathBuilder.ResolveAndVerifyWithinRoot, and
    // it needs far less defensive code: every segment reaching it has already been
    // through AcceptedPathBuilder.SanitizeSegment, which reduces the charset to
    // lowercased alphanumerics and '-'. The traversal guard below is therefore
    // belt-and-braces, not the primary control.
    public static class SmbPath
    {
        // Share-relative, backslash-separated, no leading or trailing separator.
        // The empty string is the share root, which is what CreateFile wants for it.
        public static string Normalize(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            var segments = path
                .Replace('/', SmbConstants.Separator)
                .Split(SmbConstants.Separator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in segments)
                GuardSegment(segment);

            return string.Join(SmbConstants.Separator, segments);
        }

        // BasePath + relativePath. Either side may be empty: an empty BasePath means the
        // share root is the accepted root, and an empty relative path addresses BasePath
        // itself (which is how the diagnostic lists it).
        public static string Combine(string? basePath, string? relativePath)
        {
            var root = Normalize(basePath);
            var relative = Normalize(relativePath);

            if (root.Length == 0)
                return relative;
            if (relative.Length == 0)
                return root;

            return root + SmbConstants.Separator + relative;
        }

        // The containing folder of a normalized path, or "" when the path sits at the
        // share root.
        public static string GetParent(string? path)
        {
            var normalized = Normalize(path);
            var lastSeparator = normalized.LastIndexOf(SmbConstants.Separator);

            return lastSeparator < 0 ? string.Empty : normalized[..lastSeparator];
        }

        // Every folder on the way to `path`, outermost first, including `path` itself:
        // "a\b\c" → ["a", "a\b", "a\b\c"]. SMB has no mkdir -p, so directory creation
        // walks this chain issuing FILE_OPEN_IF for each level (Stage 3).
        public static IReadOnlyList<string> EnumerateAncestors(string? path)
        {
            var normalized = Normalize(path);
            if (normalized.Length == 0)
                return [];

            var segments = normalized.Split(SmbConstants.Separator);
            var ancestors = new List<string>(segments.Length);

            for (var i = 0; i < segments.Length; i++)
                ancestors.Add(string.Join(SmbConstants.Separator, segments[..(i + 1)]));

            return ancestors;
        }

        private static void GuardSegment(string segment)
        {
            if (segment is SmbConstants.CurrentDirectoryEntry or SmbConstants.ParentDirectoryEntry)
                throw new PathTraversalException($"SMB path segment '{segment}' is not allowed.");

            // A drive-qualified or stream-qualified segment would re-root the path on the
            // server side; neither can come out of AcceptedPathBuilder, so seeing one
            // means something upstream is wrong.
            if (segment.Contains(':'))
                throw new PathTraversalException($"SMB path segment '{segment}' contains an illegal character.");
        }
    }
}
