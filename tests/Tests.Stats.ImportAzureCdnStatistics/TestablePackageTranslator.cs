// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.ImportAzureCdnStatistics;
using System.IO;
using System.Reflection;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class TestablePackageTranslator : PackageTranslator
    {
        internal override Stream GetPackageTranslationsStream()
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream("packagetranslations.json");
        }
    }
}
