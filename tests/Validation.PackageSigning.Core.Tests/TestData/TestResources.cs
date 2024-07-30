// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Validation.PackageSigning.Core.Tests.TestData
{
    internal class TestResources
    {
        private const string ResourceNamespace = "Validation.PackageSigning.Core.Tests.TestData";

        public const string TrustedCARootCertificate = ResourceNamespace + ".verisign-ca-root.cer";
        
        /// <summary>
        /// Buffer the resource stream into memory so the caller doesn't have to dispose.
        /// </summary>
        public static MemoryStream GetResourceStream(string resourceName)
        {
            var resourceStream = typeof(TestResources)
                .Assembly
                .GetManifestResourceStream(resourceName);

            if (resourceStream == null)
            {
                return null;
            }

            var bufferedStream = new MemoryStream();
            using (resourceStream)
            {
                resourceStream.CopyTo(bufferedStream);
            }

            bufferedStream.Position = 0;
            return bufferedStream;
        }

        public static X509Certificate2 GetTestCertificate(string resourceName)
        {
            var resourceStream = GetResourceStream(resourceName);
            if (resourceStream == null)
            {
                return null;
            }

            return new X509Certificate2(resourceStream.ToArray());
        }
    }
}
