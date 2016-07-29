// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGetGallery.Packaging;

namespace HandlePackageEdits
{
    public class PackageEdit
    {
        public int Key { get; set; }
        public int PackageKey { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }
        public string Hash { get; set; }

        /// <summary>
        /// User who edited the package and thereby caused this edit to be created.
        /// </summary>
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

        public string Title { get; set; }
        public string Authors { get; set; }
        public string Copyright { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public string LicenseUrl { get; set; }
        public string ProjectUrl { get; set; }
        public string ReleaseNotes { get; set; }
        public bool? RequiresLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }

        public virtual List<Action<ManifestEdit>> GetEditsAsActionList()
        {
            return new List<Action<ManifestEdit>>
            {
                (m) => { m.Authors = string.IsNullOrEmpty(Authors) ? m.Authors : Authors; },
                (m) => { m.Copyright = string.IsNullOrEmpty(Copyright) ? m.Copyright : Copyright; },
                (m) => { m.Description = string.IsNullOrEmpty(Description) ? m.Description: Description; },
                (m) => { m.IconUrl = string.IsNullOrEmpty(IconUrl) ? m.IconUrl : IconUrl; },
                (m) => { m.LicenseUrl = string.IsNullOrEmpty(LicenseUrl) ? m.LicenseUrl : LicenseUrl; },
                (m) => { m.ProjectUrl = string.IsNullOrEmpty(ProjectUrl) ? m.ProjectUrl : ProjectUrl; },
                (m) => { m.ReleaseNotes = string.IsNullOrEmpty(ReleaseNotes) ? m.ReleaseNotes : ReleaseNotes; },
                (m) => { m.RequireLicenseAcceptance = RequiresLicenseAcceptance ?? m.RequireLicenseAcceptance; },
                (m) => { m.Summary = string.IsNullOrEmpty(Summary) ? m.Summary : Summary; },
                (m) => { m.Title = string.IsNullOrEmpty(Title) ? m.Title : Title; },
                (m) => { m.Tags = string.IsNullOrEmpty(Tags) ? m.Tags : Tags; },
            };
        }
    }
}
