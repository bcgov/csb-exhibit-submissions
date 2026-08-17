namespace CES.Business.Constants
{
    public static class UserConstants
    {
        /// <summary>
        /// Ample for any badge/PIN format currently in use, and the width of the
        /// <c>ApplicationUsers.OfficerNumber</c> column.
        /// </summary>
        public const int OfficerNumberMaxLength = 30;

        /// <summary>
        /// No authoritative schema exists for officer numbers, so this is a defensive character
        /// allowlist rather than a format check: alphanumerics, dashes and periods only.
        /// </summary>
        public const string OfficerNumberPattern = @"^[A-Za-z0-9.\-]+$";
    }
}
