using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.ServiceModel
{
    public class ServiceHostDescription
    {
        public ServiceHostName ServiceHostName { get; private set; }
        public string MachineName { get; private set; }

        public ServiceHostDescription(ServiceHostName host, string machineName)
        {
            ServiceHostName = host;
            MachineName = machineName;
        }
    }
}
