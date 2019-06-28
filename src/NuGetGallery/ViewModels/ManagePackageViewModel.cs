// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Web.Mvc;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ManagePackageViewModel : ListPackageItemViewModel
    {
        public ManagePackageViewModel(Package package, User currentUser, IReadOnlyList<ReportPackageReason> reasons, UrlHelper url, string readMe, bool isManageDeprecationEnabled)
        {
            // TODO: remove
            this.SetupFromPackage(package, currentUser, reasons, url, readMe, isManageDeprecationEnabled);
        }

        public List<SelectListItem> VersionSelectList { get; set; }
        public bool IsCurrentUserAnAdmin { get; set; }
        public DeletePackagesRequest DeletePackagesRequest { get; set; }
        public bool IsLocked { get; set; }
        public EditPackageVersionReadMeRequest ReadMe { get; set; }
        public Dictionary<string, VersionListedState> VersionListedStateDictionary { get; set; }
        public Dictionary<string, VersionReadMeState> VersionReadMeStateDictionary { get; set; }
        public bool IsManageDeprecationEnabled { get; set; }
        public Dictionary<string, VersionDeprecationState> VersionDeprecationStateDictionary { get; set; }

        /// <remarks>
        /// The schema of this class is shared with the client-side Javascript to share information about package listing state.
        /// The JS expects the exact naming of its properties. Do not change the naming without updating the JS.
        /// </remarks>
        public class VersionListedState
        {
            public VersionListedState(bool listed, int downloadCount)
            {
                Listed = listed;
                DownloadCount = downloadCount;
            }

            public bool Listed { get; }
            public int DownloadCount { get; }
        }

        /// <remarks>
        /// The schema of this class is shared with the client-side Javascript to share information about package ReadMe state.
        /// The JS expects the exact naming of its properties. Do not change the naming without updating the JS.
        /// </remarks>
        public class VersionReadMeState
        {
            public VersionReadMeState(string submitUrl, string getReadMeUrl, string readMe)
            {
                SubmitUrl = submitUrl;
                GetReadMeUrl = getReadMeUrl;
                ReadMe = readMe;
            }

            public string SubmitUrl { get; }
            public string GetReadMeUrl { get; }
            public string ReadMe { get; }
        }

        /// <remarks>
        /// The schema of this class is shared with the client-side Javascript to share information about package deprecation state.
        /// The JS expects the exact naming of its properties. Do not change the naming without updating the JS.
        /// </remarks>
        public class VersionDeprecationState
        {
            public string Text { get; set; }
            public bool IsLegacy { get; set; }
            public bool HasCriticalBugs { get; set; }
            public bool IsOther { get; set; }
            public string AlternatePackageId { get; set; }
            public string AlternatePackageVersion { get; set; }
            public string CustomMessage { get; set; }
        }
    }
}