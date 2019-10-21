// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class SearchSideBySideViewModel
    {
        public const string BetterSideLabel = "Which results are better?";
        public const string MostRelevantPackageLabel = "What was the most relevant package?";
        public const string ExpectedPackagesLabel = "Name at least one package you were expecting to see.";
        public const string CommentsLabel = "Comments:";
        public const string EmailLabel = "Email (optional, provide only if you want us to be able to follow up):";

        public bool IsDisabled { get; set; }

        public string SearchTerm { get; set; } = string.Empty;

        public bool OldSuccess { get; set; }
        public int OldHits { get; set; }
        public IReadOnlyList<ListPackageItemViewModel> OldItems { get; set; } = new List<ListPackageItemViewModel>();

        public bool NewSuccess { get; set; }
        public int NewHits { get; set; }
        public IReadOnlyList<ListPackageItemViewModel> NewItems { get; set; } = new List<ListPackageItemViewModel>();

        [Display(Name = BetterSideLabel)]
        public string BetterSide { get; set; }

        [Display(Name = MostRelevantPackageLabel)]
        public string MostRelevantPackage { get; set; }

        [Display(Name = ExpectedPackagesLabel)]
        public string ExpectedPackages { get; set; }

        [Display(Name = CommentsLabel)]
        [AllowHtml]
        public string Comments { get; set; }

        [Display(Name = EmailLabel)]
        public string EmailAddress { get; set; }
    }
}