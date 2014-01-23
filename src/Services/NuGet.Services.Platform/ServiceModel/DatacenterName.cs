using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Services.ServiceModel
{
    public class DatacenterName : IEquatable<DatacenterName>
    {
        private static readonly Regex ParseRegex = new Regex("^(?<env>.*)-(?<dc>.*)$", RegexOptions.IgnoreCase);
        private static readonly string ToStringFormat = "{0}-{1}";
        
        public string Environment { get; private set; }
        public int Id { get; private set; }

        public DatacenterName(string environment, int id)
        {
            Environment = environment;
            Id = id;
        }

        public static bool TryParse(string name, out DatacenterName parsed)
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
            parsed = new DatacenterName(
                match.Groups["env"].Value,
                dc);
            return true;
        }

        public static DatacenterName Parse(string name)
        {
            DatacenterName parsed;
            if (!TryParse(name, out parsed))
            {
                throw new FormatException(Strings.DatacenterName_InvalidName);
            }
            return parsed;
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture,
                ToStringFormat,
                Environment,
                Id);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DatacenterName);
        }

        public bool Equals(DatacenterName other)
        {
            return other != null &&
                String.Equals(Environment, other.Environment, StringComparison.OrdinalIgnoreCase) &&
                Id == other.Id;
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(Environment)
                .Add(Id)
                .CombinedHash;
        }
    }
}
