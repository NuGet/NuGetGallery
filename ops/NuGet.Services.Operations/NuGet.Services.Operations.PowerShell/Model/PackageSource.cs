using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Operations.Model
{
    public class PackageSource : NuOpsComponentBase
    {
        public virtual IEnumerable<DeploymentPackage> GetPackages(string service, int? limit)
        {
            return Enumerable.Empty<DeploymentPackage>();
        }
    }
}
