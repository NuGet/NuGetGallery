// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery.Areas.Admin.ViewModels
{

    public sealed class ReservedNamespaceResultModel
    {
        public ReservedNamespace prefix { get; }

        public bool isExisting { get; }

        public string[] registrations { get; }

        public string[] owners { get; }

        public ReservedNamespaceResultModel() { }

        public ReservedNamespaceResultModel(ReservedNamespace reservedNamespace, bool isExisting)
        {
            if (reservedNamespace == null)
            {
                throw new ArgumentNullException(nameof(reservedNamespace));
            }

            prefix = new ReservedNamespace(reservedNamespace.Value, isSharedNamespace: reservedNamespace.IsSharedNamespace, isPrefix: reservedNamespace.IsPrefix);
            registrations = reservedNamespace.PackageRegistrations?.Select(pr => pr.Id).ToArray();
            owners = reservedNamespace.Owners?.Select(u => u.Username).ToArray();
            this.isExisting = isExisting;
        }
    }
}