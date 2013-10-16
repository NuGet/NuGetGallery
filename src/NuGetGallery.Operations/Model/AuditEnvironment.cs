using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Operations.Model
{
    public class AuditEnvironment
    {
        public string MachineName { get; set; }
        public string UserName { get; set; }
        public DateTime TimestampUtc { get; set; }

        public AuditEnvironment(string machineName, string userName, DateTime timeStampUtc) 
        {
            MachineName = machineName;
            UserName = userName;
            TimestampUtc = timeStampUtc;
        }

        public static AuditEnvironment GetCurrent()
        {
            return new AuditEnvironment(
                Environment.MachineName,
                String.Format(@"{0}\{1}", Environment.UserDomainName, Environment.UserName),
                DateTime.UtcNow);
        }
    }
}
