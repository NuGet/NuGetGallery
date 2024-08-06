// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Licenses;
using NuGetGallery.Helpers;

namespace NuGetGallery.ViewModels
{
    public class CompositeLicenseExpressionSegmentViewModel
    {
        public string Value { get; }
        public bool IsLicenseOrException { get; }
        public string LicenseUrl { get; }

        public CompositeLicenseExpressionSegmentViewModel(CompositeLicenseExpressionSegment segment)
        {
            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment));
            }

            Value = segment.Value;
            IsLicenseOrException = segment.Type == CompositeLicenseExpressionSegmentType.LicenseIdentifier || segment.Type == CompositeLicenseExpressionSegmentType.ExceptionIdentifier;
            if (IsLicenseOrException)
            {
                LicenseUrl = LicenseExpressionRedirectUrlHelper.GetLicenseExpressionRedirectUrl(segment.Value);
            }
            else
            {
                LicenseUrl = null;
            }
        }
    }
}