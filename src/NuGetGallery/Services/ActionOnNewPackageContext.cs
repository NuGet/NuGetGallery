// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class ActionOnNewPackageContext
    {
        public string PackageId { get; }
        public IReservedNamespaceService ReservedNamespaceService { get; }

        public ActionOnNewPackageContext(string packageId, IReservedNamespaceService reservedNamespaceService)
        {
            PackageId = packageId;
            ReservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
        }
    }
}