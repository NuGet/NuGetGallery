using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Operations.Model
{
    public abstract class AuditRecord
    {
        public AuditEnvironment Environment { get; set; }

        protected AuditRecord(AuditEnvironment environment)
        {
            Environment = environment;
        }
    }
}
