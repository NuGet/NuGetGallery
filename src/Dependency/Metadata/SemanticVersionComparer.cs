
using System.Collections.Generic;

namespace Resolver.Metadata
{
    public class SemanticVersionComparer : Comparer<SemanticVersion>
    {
        public override int Compare(SemanticVersion x, SemanticVersion y)
        {
            if (x.Major < y.Major)
            {
                return -1;
            }
            else if (x.Major > y.Major)
            {
                return 1;
            }
            else if (x.Minor < y.Minor)
            {
                return -1;
            }
            else if (x.Minor > y.Minor)
            {
                return 1;
            }
            else if (x.Patch < y.Patch)
            {
                return -1;
            }
            else if (x.Patch > y.Patch)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
    }
}
