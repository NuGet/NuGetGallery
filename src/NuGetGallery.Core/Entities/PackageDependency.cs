// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class PackageDependency : IEntity
    {
        public Package Package { get; set; }
        public int PackageKey { get; set; }

        /// <remarks>
        ///     We insert a record with a null Id to indicate an empty package dependency set. In such a case, the Id would be empty and hence
        ///     we cannot mandate that it is required.
        /// </remarks>
        [StringLength(128)]
        public string Id { get; set; }

        [StringLength(256)]
        public string VersionSpec { get; set; }

        [StringLength(256)]
        public string TargetFramework { get; set; }

        public int Key { get; set; }
    }
}