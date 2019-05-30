using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public static class ObjectExtensions
    {
        public static string ToStringOrNull(this object obj)
        {
            if (obj == null)
            {
                return null;
            }

            return obj.ToString();
        }

        public static string ToStringSafe(this object obj)
        {
            if (obj != null)
            {
                return obj.ToString();
            }
            return String.Empty;
        }
    }
}
