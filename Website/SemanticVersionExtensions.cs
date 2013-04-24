using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGet;

namespace NuGetGallery
{
    public static class SemanticVersionExtensions
    {
        public static string ToNormalizedString(this SemanticVersion self)
        {
            return String.Concat(
                self.Version.ToString());
        }
    }
}