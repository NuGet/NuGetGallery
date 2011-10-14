using System;

namespace NuGetGallery
{
    public class EntityException : Exception
    {
        public EntityException(string message)
            : base(message)
        {
        }

        public EntityException(
            string message,
            params object[] args)
            : base(string.Format(message, args))
        {
        }
    }
}