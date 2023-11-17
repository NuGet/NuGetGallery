// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGet.Services.Entities;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class PopularityTransferViewModel
    {
        public PopularityTransferViewModel()
        {
            ValidatedInputs = new List<PopularityTransferItem>();
            ExistingPackageRenamesConflict = new List<PopularityTransferItem>();
            ExistingPackageRenamesTransitive = new List<PopularityTransferItem>();
            ExistingPackageRenamesMessagesConflict = new List<string>();
            ExistingPackageRenamesMessagesTransitive = new List<string>();
            SuccessMessage = string.Empty;
        }

        public List<PopularityTransferItem> ValidatedInputs { get; set; }
        public List<PopularityTransferItem> ExistingPackageRenamesConflict { get; set; }
        public List<PopularityTransferItem> ExistingPackageRenamesTransitive { get; set; }
        public List<string> ExistingPackageRenamesMessagesConflict { get; set; }
        public List<string> ExistingPackageRenamesMessagesTransitive { get; set; }
        public string SuccessMessage { get; set; } = string.Empty;
    }

    public class PopularityTransferItem
    {
        public PopularityTransferItem()
        {
            FromOwners = new List<UserViewModel>();
            ToOwners = new List<UserViewModel>();
        }

        public PopularityTransferItem(PackageRegistration packageFrom, PackageRegistration packageTo)
        {
            FromId = packageFrom.Id;
            FromUrl = UrlHelperExtensions.Package(new UrlHelper(HttpContext.Current.Request.RequestContext), packageFrom.Id);
            FromDownloads = packageFrom.DownloadCount.ToNuGetNumberString();
            FromOwners = packageFrom
                            .Owners
                            .Select(u => u.Username)
                            .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
                            .Select(u => new UserViewModel
                                            {
                                                Username = u,
                                                ProfileUrl = UrlHelperExtensions.User(new UrlHelper(HttpContext.Current.Request.RequestContext), u),
                                            })
                            .ToList();
            FromKey = packageFrom.Key;

            ToId = packageTo.Id;
            ToUrl = UrlHelperExtensions.Package(new UrlHelper(HttpContext.Current.Request.RequestContext), packageTo.Id);
            ToDownloads = packageTo.DownloadCount.ToNuGetNumberString();
            ToOwners = packageTo
                            .Owners
                            .Select(u => u.Username)
                            .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
                            .Select(u => new UserViewModel
                                            {
                                                Username = u,
                                                ProfileUrl = UrlHelperExtensions.User(new UrlHelper(HttpContext.Current.Request.RequestContext), u),
                                            })
                            .ToList();
            ToKey = packageTo.Key;
        }

        public string FromId { get; set; }
        public string FromUrl { get; set; }
        public string FromDownloads { get; set; }
        public IReadOnlyList<UserViewModel> FromOwners { get; set; } = new List<UserViewModel>();
        public int FromKey { get; set; }

        public string ToId { get; set; }
        public string ToUrl { get; set; }
        public string ToDownloads { get; set; }
        public IReadOnlyList<UserViewModel> ToOwners { get; set; } = new List<UserViewModel>();
        public int ToKey { get; set; }
    }
}