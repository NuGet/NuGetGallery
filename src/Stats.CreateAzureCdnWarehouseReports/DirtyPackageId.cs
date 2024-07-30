// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.CreateAzureCdnWarehouseReports
{
    internal class DirtyPackageId
    {
        public DirtyPackageId(string packageId, DateTime runToCuror)
        {
            PackageId = packageId;
            RunToCuror = runToCuror;
        }

        public string PackageId { get; private set; }


        /// <summary>
        /// Represents the Timestamp of the Download Fact that is last to be processed.
        /// </summary>
        public DateTime RunToCuror { get; private set; }
    }
}