// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;

namespace NuGetGallery
{
    /// <summary>
    /// Package status as presented in Package page "Status" column
    /// </summary>
    public enum PackageStatusSummary
    {
        None,
        [Description("Validating")]
        Validating,
        [Description("Failed validation")]
        FailedValidation,
        [Description("Listed")]
        Listed,
        [Description("Unlisted")]
        Unlisted,
    }
}