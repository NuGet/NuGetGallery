// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public static class CakeBuildManagerExtensions
    {
        public static bool IsCakeExtension(this DisplayPackageViewModel model)
        {
            return IsCakeAddin(model) || IsCakeModule(model) || IsCakeRecipe(model);
        }

        public static PackageManagerViewModel.InstallPackageCommand[] GetCakeInstallPackageCommands(this DisplayPackageViewModel model)
            => model
                .EnumerateCakeInstallPackageCommands()
                .ToArray();

        public static IEnumerable<PackageManagerViewModel.InstallPackageCommand> EnumerateCakeInstallPackageCommands(this DisplayPackageViewModel model)
        {
            var scheme = model.IsDotnetToolPackageType ? "dotnet" : "nuget";
            var reference = $"{scheme}:?package={model.Id}&version={model.Version}";

            if (model.Prerelease)
            {
                reference += "&prerelease";
            }

            if (model.IsDotnetToolPackageType)
            {
                yield return new PackageManagerViewModel.InstallPackageCommand($"#tool {reference}");
            }
            else if (IsCakeAddin(model))
            {
                yield return new PackageManagerViewModel.InstallPackageCommand($"#addin {reference}");
            }
            else if (IsCakeModule(model))
            {
                yield return new PackageManagerViewModel.InstallPackageCommand($"#module {reference}");
            }
            else if (IsCakeRecipe(model))
            {
                yield return new PackageManagerViewModel.InstallPackageCommand($"#load {reference}");
            }
            else
            {
                yield return new PackageManagerViewModel.InstallPackageCommand(
                    $"Install as a Cake Addin",
                    $"#addin {reference}"
                );

                yield return new PackageManagerViewModel.InstallPackageCommand(
                    $"Install as a Cake Tool",
                    $"#tool {reference}"
                );
            };
        }

        private static bool IsCakeAddin(ListPackageItemViewModel model) =>
            model.Tags?.Contains("cake-addin", StringComparer.OrdinalIgnoreCase) ?? false;

        private static bool IsCakeModule(ListPackageItemViewModel model) =>
            model.Tags?.Contains("cake-module", StringComparer.OrdinalIgnoreCase) ?? false;

        private static bool IsCakeRecipe(ListPackageItemViewModel model) =>
            model.Tags?.Contains("cake-recipe", StringComparer.OrdinalIgnoreCase) ?? false;
    }
}
