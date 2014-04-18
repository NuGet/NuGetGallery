using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Packaging
{
    [Serializable]
    public class InvalidPackageException : Exception
    {
        public InvalidPackageException() { }
        public InvalidPackageException(string message) : base(message) { }
        public InvalidPackageException(string message, Exception inner) : base(message, inner) { }
        protected InvalidPackageException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
