using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Operations.Model
{
    public class NuOpsEnvironment
    {
        public string Name { get; set; }
        public Version Version { get; set; }
        public Subscription Subscription { get; set; }

        public IList<Datacenter> Datacenters { get; private set; }

        public Datacenter this[int id] { get { return Datacenters[id]; } }

        public NuOpsEnvironment()
        {
            Datacenters = new List<Datacenter>();
        }
    }
}
