// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;
using NuGetGallery.Features;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    [AttributeUsage(AttributeTargets.Property)]
    public class FeatureFlagsJsonValidationAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (!(value is string json))
            {
                return false;
            }

            return FeatureFlagFileStorageService.IsValidFlagsJson(json);
        }
    }
}