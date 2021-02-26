using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web;

namespace NuGetGallery
{
    public static class CakeBuildManagerExtensions
    {
        public static string GetCakeInstallPackageCommand(this DisplayPackageViewModel model)
        {
            var reference = $"nuget:?package={model.Id}&version={model.Version}";

            if (model.Prerelease)
            {
                reference += "&prerelease";
            }

            if (model.Tags.Contains("cake-addin", StringComparer.CurrentCultureIgnoreCase))
            {
                return $"#addin {reference}";
            }

            if (model.Tags.Contains("cake-module", StringComparer.CurrentCultureIgnoreCase))
            {
                return $"#module {reference}";
            }

            if (model.Tags.Contains("cake-recipe", StringComparer.CurrentCultureIgnoreCase))
            {
                return $"#load {reference}";
            }

            return string.Join(Environment.NewLine, new[]
            {
                $"// Install {model.Id} as a Cake Addin",
                $"#addin {reference}",
                "",
                $"// Install {model.Id} as a Cake Tool",
                $"#tool {reference}",
            });

        }
    }
}