using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Auditing
{
    /// <summary>
    /// Represents the actual data stored in an audit entry, an AuditRecord/AuditEnvironment pair
    /// </summary>
    public class AuditEntry
    {
        public AuditRecord Record { get; set; }
        public AuditEnvironment Environment { get; set; }

        public AuditEntry()
        {
        }

        public AuditEntry(AuditRecord record, AuditEnvironment environment)
        {
            Record = record;
            Environment = environment;
        }
    }
}
