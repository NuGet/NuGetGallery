// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public sealed class ReservedNamespaceViewModel
    {
        public string ReservedNamespacesQuery { get; set; }
        public IReadOnlyList<ReservedNamespace> ReservedNamespaces { get; set; }
        public string PackageRegistrationsQuery { get; set; }
        public IReadOnlyList<PackageRegistration> PackageRegistrations { get; set; }
    }
}