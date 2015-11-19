// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    public class TestSettings
    {
        public string PackageDirectory { get; set; }
        
        public string DataDirectory { get; set; }
        
        public string RegistrationBaseAddress { get; set; }
        
        public string ApiIndexUrl { get; set; }

        public string BaseLuceneDirectory { get; set; }
    }
}