using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Http.Models
{
    public class HostRootModel
    {
        public Uri HostInfo { get; set; }
        public object ApiDescription { get; set; }
    }
}
