using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Services.ServiceModel
{
    public class ServiceHostName : IEquatable<ServiceHostName>
    {
        private static readonly Regex ParseRegex = new Regex("^(?<env>.*)-(?<dc>.*)-(?<host>.*)_IN(?<instance>.*)$", RegexOptions.IgnoreCase);
        private static readonly string ToStringFormat = "{0}-{1}_IN{2}";
        
        public DatacenterName Datacenter { get; private set; }
        public string Name { get; private set; }
        public int InstanceId { get; private set; }

        public ServiceHostName(DatacenterName datacenter, string name, int instanceId)
        {
            Datacenter = datacenter;
            Name = name;
            InstanceId = instanceId;
        }

        public static bool TryParse(string name, out ServiceHostName parsed)
        {
            parsed = null;

            var match = ParseRegex.Match(name);
            if (!match.Success)
            {
                return false;
            }
            int dc;
            if (!Int32.TryParse(match.Groups["dc"].Value, out dc))
            {
                return false;
            }
            int instanceId;
            if (!Int32.TryParse(match.Groups["instance"].Value, out instanceId))
            {
                return false;
            }
            parsed = new ServiceHostName(
                new DatacenterName(
                    match.Groups["env"].Value,
                    dc),
                match.Groups["host"].Value,
                instanceId);
            return true;
        }

        public static ServiceHostName Parse(string name)
        {
            ServiceHostName parsed;
            if (!TryParse(name, out parsed))
            {
                throw new FormatException(Strings.ServiceHostName_InvalidName);
            }
            return parsed;
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture,
                ToStringFormat,
                Datacenter,
                Name,
                InstanceId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ServiceHostName);
        }

        public bool Equals(ServiceHostName other)
        {
            return other != null &&
                Equals(Datacenter, other.Datacenter) &&
                String.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(Datacenter)
                .Add(Name)
                .CombinedHash;
        }
    }
}
