using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Http.Models
{
    public class HostRootModel
    {
        public Uri Host { get; set; }
        public object Api { get; set; }
    }
}
