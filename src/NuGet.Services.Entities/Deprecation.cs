// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    public class Deprecation
    {
        public AlternatePackage AlternatePackage { get; set; }

        public string Message { get; set; }

        public string[] Reasons { get; set; }
    }
}
