// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    /// <summary>
    /// This is a pending package metadata edit, where a user has decided to redescribe a package and have the nupkg regenerated.
    /// </summary>
    public class PackageEdit : IEntity
    {
        public int Key { get; set; }

        public Package Package { get; set; }
        public int PackageKey { get; set; }

        /// <summary>
        /// User who edited the package and thereby caused this edit to be created.
        /// </summary>
        public User User { get; set; }
        public int UserKey { get; set; }

        /// <summary>
        /// Time this edit was generated
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Count so that the worker role can tell itself not to retry processing this edit forever if it gets stuck.
        /// </summary>
        public int TriedCount { get; set; }
        public string LastError { get; set; }

        //////////////// The rest are same as on Package ////////////

        [StringLength(256)]
        public string Title { get; set; }
        public string Authors { get; set; }
        public string Copyright { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public string LicenseUrl { get; set; }
        public string ProjectUrl { get; set; }
        public string ReleaseNotes { get; set; }
        public bool RequiresLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }

        public void Apply(string hashAlgorithm, string hash, long packageFileSize)
        {
            Package.ApplyEdit(this, hashAlgorithm, hash, packageFileSize);
        }
    }
}