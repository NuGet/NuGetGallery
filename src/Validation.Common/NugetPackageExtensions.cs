// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.Common
{
    public static class NugetPackageExtensions
    {
        public static string GetVersion(this NuGetPackage package)
        {
            return package.NormalizedVersion ?? package.Version;
        }
    }
}
