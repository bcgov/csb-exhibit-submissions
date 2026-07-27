namespace CES.Business.Constants
{
    public static class ClassificationConstants
    {
        public const int ClassificationEditWindowSeconds = 10;
        // Maximum length of a single (multiline) description entry — CES-42.
        public const int DescriptionMaxLength = 1000;
        public const string MarkedMin = "A";
        public const string MarkedMax = "Z";
        public const int EnteredMin = 1;
        public const int EnteredMax = 50;
        public static readonly string[] EvidenceSourceTypes = { "BodyCam", "DashCam", "Other" };
    }
}
