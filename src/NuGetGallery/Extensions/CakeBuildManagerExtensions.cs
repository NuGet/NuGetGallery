// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NuGetGallery
{
    public static class CakeBuildManagerExtensions
    {
        public static bool IsCakeExtension(this DisplayPackageViewModel model)
        {
            return IsCakeAddin(model) || IsCakeModule(model) || IsCakeRecipe(model);
        }

        public static string GetCakeInstallPackageCommand(this DisplayPackageViewModel model)
        {
            var scheme = model.IsDotnetToolPackageType ? "dotnet" : "nuget";
            var reference = $"{scheme}:?package={model.Id}&version={model.Version}";

            if (model.Prerelease)
            {
                reference += "&prerelease";
            }

            if (model.IsDotnetToolPackageType)
            {
                return $"#tool {reference}";
            }

            if (IsCakeAddin(model))
            {
                return $"#addin {reference}";
            }

            if (IsCakeModule(model))
            {
                return $"#module {reference}";
            }

            if (IsCakeRecipe(model))
            {
                return $"#load {reference}";
            }

            return string.Join(Environment.NewLine,
                $"// Install {model.Id} as a Cake Addin",
                $"#addin {reference}",
                "",
                $"// Install {model.Id} as a Cake Tool",
                $"#tool {reference}"
            );
        }

        private static bool IsCakeAddin(ListPackageItemViewModel model) =>
            model.Tags?.Contains("cake-addin", StringComparer.OrdinalIgnoreCase) ?? false;

        private static bool IsCakeModule(ListPackageItemViewModel model) =>
            model.Tags?.Contains("cake-module", StringComparer.OrdinalIgnoreCase) ?? false;

        private static bool IsCakeRecipe(ListPackageItemViewModel model) =>
            model.Tags?.Contains("cake-recipe", StringComparer.OrdinalIgnoreCase) ?? false;
    }
}
