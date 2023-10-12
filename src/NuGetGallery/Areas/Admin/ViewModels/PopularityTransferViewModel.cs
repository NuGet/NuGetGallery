// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class PopularityTransferViewModel
    {
        public PopularityTransferViewModel()
        {
            ValidatedInputs = new List<PopularityTransferItem>();
            ExistingPackageRenames = new List<string>();
            SuccessMessage = string.Empty;
        }

        public List<PopularityTransferItem> ValidatedInputs { get; set; }
        public List<string> ExistingPackageRenames { get; set; }
        public string SuccessMessage { get; set; } = string.Empty;
    }

    public class PopularityTransferItem
    {
        public PopularityTransferItem()
        {
            FromOwners = new List<UserViewModel>();
            ToOwners = new List<UserViewModel>();
        }

        public PopularityTransferItem(
            PackageSearchResult packageFrom,
            PackageSearchResult packageTo,
            long fromDownloads,
            long toDownloads,
            int fromKey,
            int toKey)
        {
            FromId = packageFrom.PackageId;
            FromUrl = UrlHelperExtensions.Package(new UrlHelper(HttpContext.Current.Request.RequestContext), packageFrom.PackageId);
            FromDownloads = fromDownloads;
            FromOwners = packageFrom.Owners;
            FromKey = fromKey;

            ToId = packageTo.PackageId;
            ToUrl = UrlHelperExtensions.Package(new UrlHelper(HttpContext.Current.Request.RequestContext), packageTo.PackageId);
            ToDownloads = toDownloads;
            ToOwners = packageTo.Owners;
            ToKey = toKey;
        }

        public string FromId { get; set; }
        public string FromUrl { get; set; }
        public long FromDownloads { get; set; }
        public IReadOnlyList<UserViewModel> FromOwners { get; set; } = new List<UserViewModel>();
        public int FromKey { get; set; }

        public string ToId { get; set; }
        public string ToUrl { get; set; }
        public long ToDownloads { get; set; }
        public IReadOnlyList<UserViewModel> ToOwners { get; set; } = new List<UserViewModel>();
        public int ToKey { get; set; }
    }
}