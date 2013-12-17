using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.ServiceModel;

namespace NuGet.Services.Http.Models
{
    public class HostResponseModel
    {
        public string Environment { get; set; }
        public int Datacenter { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }
        public string MachineName { get; set; }

        public AssemblyResponseModel Runtime { get; set; }

        public Uri Services { get; set; }
        public Uri Processes { get; set; }
        public Uri Tracing { get; set; }

        public HostResponseModel() { }
        public HostResponseModel(ServiceHostDescription hostDesc, AssemblyResponseModel runtime)
        {
            Environment = hostDesc.ServiceHostName.Datacenter.Environment;
            Datacenter = hostDesc.ServiceHostName.Datacenter.Id;
            Name = hostDesc.ServiceHostName.Name;
            FullName = hostDesc.ServiceHostName.ToString();
            MachineName = hostDesc.MachineName;

            Runtime = runtime;
        }
    }
}
