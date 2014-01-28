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

        public IDictionary<int, Datacenter> Datacenters { get; private set; }
        public IList<PackageSource> PackageSources { get; private set; }
        public IList<SecretStore> SecretStores { get; private set; }
        
        public Datacenter this[int id] { get { return Datacenters[id]; } }

        public NuOpsEnvironment()
        {
            PackageSources = new List<PackageSource>();
            SecretStores = new List<SecretStore>();
            Datacenters = new Dictionary<int, Datacenter>();
        }

        public Datacenter GetDatacenter(int id)
        {
            Datacenter dc;
            if (!Datacenters.TryGetValue(id, out dc))
            {
                return null;
            }
            return dc;
        }
    }
}
