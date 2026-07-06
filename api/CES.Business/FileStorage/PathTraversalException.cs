namespace CES.Business.FileStorage
{
    // Thrown when a resolved accepted-store path would escape the configured
    // AcceptedPath root, or when a path segment fails validation. Typed so callers
    // can fail safe rather than swallowing a generic exception.
    public class PathTraversalException : Exception
    {
        public PathTraversalException(string message) : base(message) { }
    }
}
