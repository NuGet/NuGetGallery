using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Http.Models;

namespace NuGet.Services.Jobs.Api.Models
{
    public class JobsManagementServiceModel : HostRootModel
    {
        public Uri Invocations { get; set; }
        public Uri Jobs { get; set; }
    }
}
