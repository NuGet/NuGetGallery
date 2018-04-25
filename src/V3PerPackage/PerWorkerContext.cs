// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.V3PerPackage
{
    /// <summary>
    /// A child context of <see cref="PerProcessContext"/> and a parent context of <see cref="PerBatchContext"/>.
    /// This is contains information used by <see cref="PerWorkerProcessor"/>.
    /// </summary>
    public class PerWorkerContext
    {
        public PerWorkerContext(PerProcessContext processContext, string workerName)
        {
            Process = processContext;
            Name = workerName;
        }

        public GlobalContext Global => Process.Global;
        public PerProcessContext Process { get; }
        public string Name { get; }

        public string CatalogStoragePath => Name;
        public string RegistrationLegacyStoragePath => $"{Name}/legacy";
        public string RegistrationCompressedStoragePath => $"{Name}/gz";
        public string RegistrationSemVer2StoragePath => $"{Name}/gz-semver2";
    }
}
