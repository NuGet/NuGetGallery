// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using NuGet.Protocol.Registration;
using NuGet.Versioning;

namespace NuGet.Jobs.Catalog2Registration
{
    /// <summary>
    /// A class that handled the bookkeeping of a leaf item. The leaf item has minimal bookkeeping required except
    /// maintaining a parsed version object for easy comparison.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class LeafInfo
    {
        private string DebuggerDisplay => $"Leaf {Version.ToNormalizedString()}";

        private LeafInfo(NuGetVersion version, RegistrationLeafItem leafItem)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
            LeafItem = leafItem ?? throw new ArgumentNullException(nameof(leafItem));
        }

        public static LeafInfo New(NuGetVersion version)
        {
            return new LeafInfo(version, new RegistrationLeafItem
            {
                CatalogEntry = new RegistrationCatalogEntry
                {
                    Version = version.ToFullString(),
                }
            });
        }

        public static LeafInfo Existing(RegistrationLeafItem leafItem)
        {
            return new LeafInfo(
                NuGetVersion.Parse(leafItem.CatalogEntry.Version),
                leafItem);
        }

        public NuGetVersion Version { get; }
        public RegistrationLeafItem LeafItem { get; }
    }
}
