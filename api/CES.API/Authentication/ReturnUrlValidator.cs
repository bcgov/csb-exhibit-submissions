namespace CES.API.Authentication
{
    /// <summary>
    /// Open-redirect guard for the <c>returnUrl</c> carried through the login round-trip.
    /// Only same-site relative paths survive; everything else collapses to the app root.
    /// </summary>
    public static class ReturnUrlValidator
    {
        /// <summary>
        /// Returns <paramref name="returnUrl"/> when it is a safe same-site relative path,
        /// otherwise <see cref="AuthConstants.DefaultReturnUrl"/>.
        /// </summary>
        public static string Sanitize(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
                return AuthConstants.DefaultReturnUrl;

            // Validate the decoded form too, so an encoded "/%2Fevil.example" cannot slip a
            // protocol-relative URL past the prefix checks below.
            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(returnUrl);
            }
            catch (UriFormatException)
            {
                return AuthConstants.DefaultReturnUrl;
            }

            return IsSafeRelativePath(returnUrl) && IsSafeRelativePath(decoded)
                ? returnUrl
                : AuthConstants.DefaultReturnUrl;
        }

        private static bool IsSafeRelativePath(string candidate)
        {
            // Must be a rooted path: this alone rejects "https://evil.example" and "javascript:…".
            if (candidate.Length == 0 || candidate[0] != '/')
                return false;

            // "//evil.example" is protocol-relative and "/\evil.example" is treated the same
            // way by several browsers — both leave the site.
            if (candidate.Length > 1 && (candidate[1] == '/' || candidate[1] == '\\'))
                return false;

            // A backslash anywhere can be normalized to "/" by the browser, and control
            // characters allow header/URL smuggling. Neither belongs in an app route.
            foreach (var c in candidate)
            {
                if (c == '\\' || char.IsControl(c))
                    return false;
            }

            return true;
        }
    }
}
