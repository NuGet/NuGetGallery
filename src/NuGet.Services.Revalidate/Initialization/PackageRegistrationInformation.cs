// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Revalidate
{
    public class PackageRegistrationInformation
    {
        public int Key { get; set; }

        public string Id { get; set; }
        public long Downloads { get; set; }
        public int Versions { get; set; }
    }
}
