using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs.Api
{
    internal static class Routes
    {
        public const string GetInvocations = "Jobs-Invocations-GetAll";
        public const string GetSingleInvocation = "Jobs-Invocations-GetOne";
        public const string GetInvocationLog = "Jobs-Invocations-GetLog";
        public const string PutInvocation = "Jobs-Invocations-Put";
        public const string GetJobs = "Jobs-Jobs-GetAll";
    }
}
