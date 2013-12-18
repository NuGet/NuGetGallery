using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs.Api.Models
{
    public class InvocationRequestModel
    {
        public string Job { get; set; }
        public string Source { get; set; }
        public Dictionary<string, string> Payload { get; set; }
    }
}
