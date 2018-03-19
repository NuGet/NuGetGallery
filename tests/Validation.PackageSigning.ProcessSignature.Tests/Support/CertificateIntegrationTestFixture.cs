// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit.Abstractions;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    using CoreCertificateIntegrationTestFixture = Core.Tests.Support.CertificateIntegrationTestFixture;

    /// <summary>
    /// This test fixture trusts the root certificate of checked in signed packages. This handles adding and removing
    /// the root certificate from the local machine trusted roots. Any tests with this fixture require admin elevation.
    /// </summary>
    public class CertificateIntegrationTestFixture : CoreCertificateIntegrationTestFixture
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private byte[] _signedPackageBytes1;

        public async Task<SignedPackageArchive> GetSignedPackage1Async(ITestOutputHelper output) => await GetSignedPackageAsync(
            new Reference<byte[]>(
                () => _signedPackageBytes1,
                x => _signedPackageBytes1 = x),
            TestResources.SignedPackageLeaf1,
            await GetSigningCertificateAsync(),
            output);
        public async Task<MemoryStream> GetSignedPackageStream1Async(ITestOutputHelper output) => await GetSignedPackageStreamAsync(
            new Reference<byte[]>(
                () => _signedPackageBytes1,
                x => _signedPackageBytes1 = x),
            TestResources.SignedPackageLeaf1,
            await GetSigningCertificateAsync(),
            output);

        private async Task<MemoryStream> GetSignedPackageStreamAsync(
            Reference<byte[]> reference,
            string resourceName,
            X509Certificate2 certificate,
            ITestOutputHelper output)
        {
            await _lock.WaitAsync();
            try
            {
                if (reference.Value == null)
                {
                    reference.Value = await GenerateSignedPackageBytesAsync(
                        resourceName,
                        certificate,
                        await GetTimestampServiceUrlAsync(),
                        output);
                }

                var memoryStream = new MemoryStream();
                memoryStream.Write(reference.Value, 0, reference.Value.Length);
                return memoryStream;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<SignedPackageArchive> GetSignedPackageAsync(
            Reference<byte[]> reference,
            string resourceName,
            X509Certificate2 certificate,
            ITestOutputHelper output)
        {
            return new SignedPackageArchive(
                await GetSignedPackageStreamAsync(reference, resourceName, certificate, output),
                new MemoryStream());
        }
        
        public async Task<byte[]> GenerateSignedPackageBytesAsync(
            string resourceName,
            X509Certificate2 certificate,
            Uri timestampUri,
            ITestOutputHelper output)
        {
            var testLogger = new TestLogger(output);
            var timestampProvider = new Rfc3161TimestampProvider(timestampUri);
            var signatureProvider = new X509SignatureProvider(timestampProvider);

            var unsignedBytes = await OperateOnSignerAsync(
                TestResources.GetResourceStream(resourceName),
                signatureProvider,
                x => x.RemoveSignaturesAsync(testLogger, CancellationToken.None));

            var signedBytes = await OperateOnSignerAsync(
                new MemoryStream(unsignedBytes),
                signatureProvider,
                x =>
                {
                    var request = new AuthorSignPackageRequest(certificate, HashAlgorithmName.SHA256);
                    return x.SignAsync(request, testLogger, CancellationToken.None);
                });

            return signedBytes;
        }

        private static async Task<byte[]> OperateOnSignerAsync(
            Stream packageReadStream,
            X509SignatureProvider signatureProvider,
            Func<Signer, Task> executeAsync)
        {
            using (packageReadStream)
            using (var packageWriteStream = new MemoryStream())
            {
                packageReadStream.CopyTo(packageWriteStream);

                using (var signedPackage = new SignedPackageArchive(packageReadStream, packageWriteStream))
                {
                    var signer = new Signer(signedPackage, signatureProvider);

                    await executeAsync(signer);

                    return packageWriteStream.ToArray();
                }
            }
        }

        /// <summary>
        /// This is a workaround for the lack of <code>ref</code> parameters in <code>async</code> methods.
        /// </summary>
        /// <typeparam name="T">The type of the reference.</typeparam>
        private class Reference<T>
        {
            private readonly Func<T> _getValue;
            private readonly Action<T> _setValue;

            public Reference(Func<T> getValue, Action<T> setValue)
            {
                _getValue = getValue;
                _setValue = setValue;
            }

            public T Value
            {
                get => _getValue();
                set => _setValue(value);
            }
        }
    }
}
