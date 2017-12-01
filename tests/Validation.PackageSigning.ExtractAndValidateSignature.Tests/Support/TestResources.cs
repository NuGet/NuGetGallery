// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    public static class TestResources
    {
        private const string ResourceNamespace = "Validation.PackageSigning.ExtractAndValidateSignature.Tests.TestData";

        public const string UnsignedPackage = ResourceNamespace + ".TestUnsigned.1.0.0.nupkg";
        public const string SignedPackage1 = ResourceNamespace + ".TestSigned.leaf-1.1.0.0.nupkg";
        public const string SignedPackage2 = ResourceNamespace + ".TestSigned.leaf-2.2.0.0.nupkg";
    }
}
