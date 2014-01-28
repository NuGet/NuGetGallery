using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;

namespace NuGet.Services.Operations.Commands
{
    public class GetNuOpsPackageCommand : OpsEnvironmentCmdlet
    {
        public static readonly int DefaultLimit = 10;

        [Parameter(Mandatory=false)]
        public string Service { get; set; }

        [Parameter(Mandatory=false)]
        public string FromSource { get; set; }

        [Parameter(Mandatory = false)]
        public int? Limit { get; set; }
        
        [Parameter(Mandatory = false)]
        public bool NoLimit { get; set; }

        protected override void ProcessRecord()
        {
            if(Limit == null && !NoLimit) {
                Limit = DefaultLimit;
            }

            var env = GetEnvironment(required: true);

            var sources = env.PackageSources;
            if (!String.IsNullOrEmpty(FromSource))
            {
                var pattern = new WildcardPattern(FromSource);
                sources = env.PackageSources.Where(s => pattern.IsMatch(s.Name));
            }
            foreach (var source in sources)
            {
                WriteObject(source.GetPackages(Service, Limit));
            }
        }
    }
}
