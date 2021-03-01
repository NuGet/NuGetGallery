// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

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

            if (model.Tags.Contains("cake-addin", StringComparer.OrdinalIgnoreCase))
            {
                return $"#addin {reference}";
            }

            if (model.Tags.Contains("cake-module", StringComparer.OrdinalIgnoreCase))
            {
                return $"#module {reference}";
            }

            if (model.Tags.Contains("cake-recipe", StringComparer.OrdinalIgnoreCase))
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
