using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace NuGetGallery.Operations
{
    [Serializable]
    public class CommandLineException : Exception
    {
        public CommandLineException()
        {
        }

        public CommandLineException(string message)
            : base(message)
        {
        }

        public CommandLineException(string format, params object[] args)
            : base(String.Format(CultureInfo.CurrentCulture, format, args))
        {
        }

        public CommandLineException(string message, Exception exception)
            : base(message, exception)
        {
        }

        protected CommandLineException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
