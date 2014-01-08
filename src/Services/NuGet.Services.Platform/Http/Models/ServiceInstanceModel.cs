using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Services.Models;
using NuGet.Services.ServiceModel;

namespace NuGet.Services.Http.Models
{
    public class ServiceInstanceModel
    {
        public string Name { get; set; }
        public string Service { get; set; }
        public DateTimeOffset? LastHeartbeat { get; set; }
        public object Description { get; set; }
        public object Status { get; set; }

        public ServiceInstanceModel() { }
        public ServiceInstanceModel(NuGetService service, object description, object status) : this()
        {
            Name = service.InstanceName.ToString();
            Service = service.ServiceName;
            LastHeartbeat = service.LastHeartbeat;
            Description = description;
            Status = status;
        }
    }
}
