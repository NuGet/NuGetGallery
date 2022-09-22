// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Mvc;

namespace NuGetGallery
{
    public static class FilterHelper
    {
        private static readonly Lazy<IFeatureFlagService> _featureFlagService
            = new Lazy<IFeatureFlagService>(() => DependencyResolver.Current.GetService<IFeatureFlagService>());

        public static bool EvaluateFilterConditions(FilterConditions conditions)
        {
            if ((conditions & FilterConditions.AreAnonymousUploadsEnabled) == FilterConditions.AreAnonymousUploadsEnabled
                    && _featureFlagService.Value.AreAnonymousUploadsEnabled())
            {
                return true;
            }

            // add additional conditions as required

            return false;
        }
    }

    [Flags]
    public enum FilterConditions
    {
        AreAnonymousUploadsEnabled = 1,
        // add additional conditions as required
    }
}