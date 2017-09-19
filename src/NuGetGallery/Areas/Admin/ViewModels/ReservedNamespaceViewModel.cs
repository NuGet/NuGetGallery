// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGetGallery.Areas.Admin.ViewModels
{

    public class ReservedNamespaceViewModel
    {
        public ReservedNamespace prefix;

        public bool isExisting;

        public string[] registrations;

        public string[] owners;

        public ReservedNamespaceViewModel(): this(null, false) { }

        public ReservedNamespaceViewModel(ReservedNamespace reservedNamespace, bool isExisting)
        {
            if (reservedNamespace != null)
            {
                prefix = new ReservedNamespace(reservedNamespace.Value, isSharedNamespace: reservedNamespace.IsSharedNamespace, isPrefix: reservedNamespace.IsPrefix);
                registrations = reservedNamespace.PackageRegistrations?.Select(pr => pr.Id).ToArray();
                owners = reservedNamespace.Owners?.Select(u => u.Username).ToArray();
            }

            this.isExisting = isExisting;
        }
    }
}