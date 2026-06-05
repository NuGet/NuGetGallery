// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public enum UpdateListedServiceResult
    {
        Success,
        PackageNotFound,
    }

    public interface IUpdateListedService
    {
        /// <summary>
        /// Updates the listed status for the specified packages. Packages that are deleted or
        /// failed validation are skipped. Returns per-package results indicating success or not-found.
        /// </summary>
        Task<IReadOnlyList<UpdateListedPackageResult>> UpdateListedAsync(
            IReadOnlyList<UpdateListedPackageIdentity> packages,
            bool listed,
            string reason = null,
            string callerIdentity = null);
    }

    public class UpdateListedPackageIdentity
    {
        public string Id { get; set; }
        public string Version { get; set; }
    }

    public class UpdateListedPackageResult
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public UpdateListedServiceResult Result { get; set; }
    }
}
