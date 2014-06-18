using System;

namespace NuGetGallery.Authentication
{
    [Serializable]
    public class FeedOnlyModeException : Exception
    {
        public const string FeedOnlyModeError = "Illegal request! Running on Feed Only mode. User Registration or authentication is disallowed.";
        public FeedOnlyModeException() { }

        public FeedOnlyModeException(string message) : base(message) { }
    }
}
