using System;
using System.Globalization;

namespace NuGetGallery
{
    [Serializable]
    public class EntityException : Exception
    {
        public EntityException(string message)
            : base(message)
        {
        }

        public EntityException(
            string message,
            params object[] args)
            : base(String.Format(CultureInfo.CurrentCulture, message, args))
        {
        }
    }
}