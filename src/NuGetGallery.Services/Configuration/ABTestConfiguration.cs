// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGetGallery.Services
{
    public class ABTestConfiguration : IABTestConfiguration
    {
        public ABTestConfiguration()
        {
            PreviewSearchPercentage = 0;
            PreviewHijackPercentage = 0;
        }

        [JsonConstructor]
        public ABTestConfiguration(int previewSearchPercentage, int previewHijackPercentage)
        {
            GuardPercentage(previewSearchPercentage, nameof(previewSearchPercentage));
            GuardPercentage(previewHijackPercentage, nameof(previewHijackPercentage));

            PreviewSearchPercentage = previewSearchPercentage;
            PreviewHijackPercentage = previewHijackPercentage;
        }

        public int PreviewSearchPercentage { get; }
        public int PreviewHijackPercentage { get; }

        private static void GuardPercentage(int value, string paramName)
        {
            if (value < 0 || value > 100)
            {
                throw new ArgumentOutOfRangeException(paramName, "Percentages must be between 0 and 100, inclusive.");
            }
        }
    }
}