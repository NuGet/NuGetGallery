// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Policy;
using System.Web;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class PopularityTransferViewModel
    {
        public PopularityTransferViewModel()
        {
            PackagesFromResult = new List<Package>();
            PackagesToResult = new List<Package>();

            TempPackagesFromResult = new List<string>();
            TempPackagesToResult = new List<string>();

            Input = new PopularityTransferInput();
        }

        [Required(ErrorMessage = "You must provide at least one package ID.")]
        public string PackagesFromInput { get; set; }
        [Required(ErrorMessage = "You must provide at least one package ID.")]
        public string PackagesToInput { get; set; }

        public List<Package> PackagesFromResult { get; set; }
        public List<Package> PackagesToResult { get; set; }

        public List<string> TempPackagesFromResult { get; set; }
        public List<string> TempPackagesToResult { get; set; }

        public PopularityTransferInput Input { get; set; }
    }

    public class ValidatedInputsResult
    {
        public ValidatedInputsResult()
        {
            ValidatedInputs = new List<ValidatedInput>();
        }

        public List<ValidatedInput> ValidatedInputs { get; set; }
        public string Warnings { get; set; }
    }

    public class ValidatedInput
    {
        public ValidatedInput(string packageFrom, string packageTo)
        {
            FromId = packageFrom;
            FromUrl = UrlHelperExtensions.Package(new UrlHelper(HttpContext.Current.Request.RequestContext), packageFrom);
            FromDownloads = 0;

            ToId = packageTo;
            ToUrl = UrlHelperExtensions.Package(new UrlHelper(HttpContext.Current.Request.RequestContext), packageTo);
            ToDownloads = 0;
        }

        public ValidatedInput(PackageSearchResult packageFrom, PackageSearchResult packageTo)
        {
            FromId = packageFrom.PackageId;
            FromUrl = UrlHelperExtensions.Package(new UrlHelper(HttpContext.Current.Request.RequestContext), packageFrom.PackageId);
            FromDownloads = packageFrom.DownloadCount;
            FromOwners = packageFrom.Owners;

            ToId = packageTo.PackageId;
            ToUrl = UrlHelperExtensions.Package(new UrlHelper(HttpContext.Current.Request.RequestContext), packageTo.PackageId);
            ToDownloads = packageTo.DownloadCount;
            ToOwners = packageTo.Owners;
        }

        public string FromId { get; set; }
        public string FromUrl { get; set; }
        public long FromDownloads { get; set; }
        public IReadOnlyList<UserViewModel> FromOwners { get; set; } = new List<UserViewModel>();

        public string ToId { get; set; }
        public string ToUrl { get; set; }
        public long ToDownloads { get; set; }
        public IReadOnlyList<UserViewModel> ToOwners { get; set; } = new List<UserViewModel>();
    }
}