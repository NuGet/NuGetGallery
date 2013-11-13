using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Auditing
{
    public class AuditEnvironment
    {
        public string MachineName { get; set; }
        public string UserName { get; set; }
        public string AuthenticationType { get; set; }
        public DateTime TimestampUtc { get; set; }

        public AuditEnvironment(string machineName, string userName, string authenticationType, DateTime timeStampUtc) 
        {
            MachineName = machineName;
            UserName = userName;
            AuthenticationType = authenticationType;
            TimestampUtc = timeStampUtc;
        }

        public static AuditEnvironment GetCurrent()
        {
            return new AuditEnvironment(
                Environment.MachineName,
                String.Format(@"{0}\{1}", Environment.UserDomainName, Environment.UserName),
                "MachineUser",
                DateTime.UtcNow);
        }
    }
}
