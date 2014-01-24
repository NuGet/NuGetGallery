using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Services.ServiceModel
{
    public class ServiceName : IEquatable<ServiceName>
    {
        private const string InstanceNameDataName = "_NuGet_ServiceInstanceName";

        private static readonly Regex ParseRegex = new Regex("^(?<env>.*)-(?<dc>.*)-(?<host>.*)_IN(?<instance>.*)-(?<service>.*)$", RegexOptions.IgnoreCase);
        private static readonly string ToStringFormat = "{0}-{1}";
        
        public ServiceHostName Host { get; private set; }
        public string Service { get; private set; }

        public ServiceName(ServiceHostName host, string service)
        {
            Host = host;
            Service = service;
        }

        public static bool TryParse(string name, out ServiceName parsed)
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
            parsed = new ServiceName(
                new ServiceHostName(
                    new DatacenterName(
                        match.Groups["env"].Value,
                        dc),
                    match.Groups["host"].Value,
                    instanceId),
                match.Groups["service"].Value);
            return true;
        }

        public static ServiceName Parse(string name)
        {
            ServiceName parsed;
            if (!TryParse(name, out parsed))
            {
                throw new FormatException(Strings.ServiceInstanceName_InvalidName);
            }
            return parsed;
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture,
                ToStringFormat,
                Host,
                Service);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ServiceName);
        }

        public bool Equals(ServiceName other)
        {
            return other != null &&
                Equals(Host, other.Host) &&
                String.Equals(Service, other.Service, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(Host)
                .Add(Service)
                .CombinedHash;
        }
    }
}
