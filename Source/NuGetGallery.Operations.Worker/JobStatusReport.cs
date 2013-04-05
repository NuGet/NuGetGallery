using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Worker
{
    class JobStatusReport
    {
        public string JobName { get; set; }
        public string At { get; set; }
        public string Duration { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }
}
