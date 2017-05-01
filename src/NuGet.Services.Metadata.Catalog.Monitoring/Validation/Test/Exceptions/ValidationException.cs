using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Base class for exceptions throw by <see cref="IValidator.Run(ValidationContext)"/>.
    /// </summary>
    public class ValidationException : Exception
    {
        public ValidationException()
            : base()
        {
        }

        public ValidationException(string message)
            : base(message)
        {
        }
    }
}
