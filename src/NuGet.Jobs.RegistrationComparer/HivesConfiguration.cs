// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.RegistrationComparer
{
    public class HivesConfiguration
    {
        public string LegacyBaseUrl { get; set; }
        public string GzippedBaseUrl { get; set; }
        public string SemVer2BaseUrl { get; set; }
    }
}
