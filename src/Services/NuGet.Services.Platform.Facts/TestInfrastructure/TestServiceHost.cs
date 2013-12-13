using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.ServiceModel;

namespace NuGet.Services.TestInfrastructure
{
    public class TestServiceHost : ServiceHost
    {
        public override ServiceHostDescription Description
        {
            get
            {
                return new ServiceHostDescription(
                    new ServiceHostName(
                        new DatacenterName(
                            "local",
                            42),
                        "testhost"),
                    "testmachine");
            }
        }

        protected override void InitializePlatformLogging()
        {
        }

        protected override IEnumerable<Type> GetServices()
        {
            return Enumerable.Empty<Type>();
        }
    }
}
