using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    public static class Facets
    {
        public static readonly string FieldName = "Facet";

        public static class Types
        {
            public static readonly string Compatible = "c";
            public static readonly string LatestStableVersion = "l_s";
            public static readonly string LatestPrereleaseVersion = "l_pre";
        }

        public static readonly string PrereleaseVersion = "pre";
        public static readonly string Listed = "listed";

        public static string Compatible(FrameworkName framework)
        {
            return Create(Types.Compatible, FrameworkNameToParameter(framework));
        }

        public static string LatestStableVersion(FrameworkName framework)
        {
            return Create(Types.LatestStableVersion, FrameworkNameToParameter(framework));
        }

        public static string LatestPrereleaseVersion(FrameworkName framework)
        {
            return Create(Types.LatestPrereleaseVersion, FrameworkNameToParameter(framework));
        }

        private static string FrameworkNameToParameter(FrameworkName framework)
        {
            return (framework == FrameworksList.AnyFramework || framework == null) ?
                String.Empty :
                VersionUtility.GetShortFrameworkName(framework);
        }

        internal static string Create(string name, string parameter)
        {
            return name + "<" + parameter + ">";
        }
    }
}
