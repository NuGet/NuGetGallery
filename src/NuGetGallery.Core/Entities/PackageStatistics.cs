// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    // IMPORTANT:   Removed the TimeStamp column from this class because 
    //              it's completely tracked by the database layer. Don't
    //              add it back! :) It will be created by the migration.
    public class PackageStatistics : IEntity
    {
        public Package Package { get; set; }
        public int PackageKey { get; set; }
        public string IPAddress { get; set; }
        public string UserAgent { get; set; }
        public int Key { get; set; }
        
        [StringLength(128)] // must be at least long enough to handle string 'Reinstall-Dependency'
        public string Operation { get; set; }

        [StringLength(128)] // max package ID length
        public string DependentPackage { get; set; }

        public string ProjectGuids { get; set; }
    }
}