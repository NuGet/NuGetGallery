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

        public async Task<SignedPackageArchive> GetAuthorSignedPackage1Async(ITestOutputHelper output) => await GetAuthorSignedPackageAsync(
            new Reference<byte[]>(
                () => _signedPackageBytes1,
                x => _signedPackageBytes1 = x),
            TestResources.SignedPackageLeaf1,
            await GetSigningCertificateAsync(),
            output);
        public async Task<MemoryStream> GetAuthorSignedPackageStream1Async(ITestOutputHelper output) => await GetAuthorSignedPackageStreamAsync(
            new Reference<byte[]>(
                () => _signedPackageBytes1,
                x => _signedPackageBytes1 = x),
            TestResources.SignedPackageLeaf1,
            await GetSigningCertificateAsync(),
            output);

        private async Task<MemoryStream> GetAuthorSignedPackageStreamAsync(
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
                    reference.Value = await GenerateAuthorSignedPackageBytesAsync(
                        resourceName,
                        certificate,
                        await GetTimestampServiceUrlAsync(),
                        output);
                }

                var memoryStream = new MemoryStream();
                await memoryStream.WriteAsync(reference.Value, 0, reference.Value.Length);
                return memoryStream;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<SignedPackageArchive> GetAuthorSignedPackageAsync(
            Reference<byte[]> reference,
            string resourceName,
            X509Certificate2 certificate,
            ITestOutputHelper output)
        {
            return new SignedPackageArchive(
                await GetAuthorSignedPackageStreamAsync(reference, resourceName, certificate, output),
                Stream.Null);
        }

        public async Task<byte[]> GenerateSignedPackageBytesAsync(
            Stream inputPackageStream,
            SignPackageRequest request,
            Uri timestampUri,
            ITestOutputHelper output)
        {
            Rfc3161TimestampProvider timestampProvider = null;
            if (timestampUri != null)
            {
                timestampProvider = new Rfc3161TimestampProvider(timestampUri);
            }

            var testLogger = new TestLogger(output);
            var signatureProvider = new X509SignatureProvider(timestampProvider);

            using (var outputPackageStream = new MemoryStream())
            {
                await SigningUtility.SignAsync(
                    new SigningOptions(
                        inputPackageStream: new Lazy<Stream>(() => inputPackageStream),
                        outputPackageStream: new Lazy<Stream>(() => outputPackageStream),
                        overwrite: true,
                        signatureProvider: signatureProvider,
                        logger: testLogger),
                    request,
                    CancellationToken.None);

                return outputPackageStream.ToArray();
            }
        }

        public Task<byte[]> GenerateAuthorSignedPackageBytesAsync(
            string resourceName,
            X509Certificate2 certificate,
            Uri timestampUri,
            ITestOutputHelper output)
        {
            return GenerateSignedPackageBytesAsync(
                TestResources.GetResourceStream(resourceName),
                new AuthorSignPackageRequest(certificate, HashAlgorithmName.SHA256),
                timestampUri,
                output);
        }

        public async Task<MemoryStream> AuthorSignPackageStreamAsync(
            Stream inputPackageStream,
            X509Certificate2 signingCertificate,
            ITestOutputHelper output)
        {
            var timestampUri = await GetTimestampServiceUrlAsync();

            var packageBytes = await GenerateSignedPackageBytesAsync(
                inputPackageStream,
                new AuthorSignPackageRequest(signingCertificate, HashAlgorithmName.SHA256),
                timestampUri,
                output);

            var memoryStream = new MemoryStream();
            await memoryStream.WriteAsync(packageBytes, 0, packageBytes.Length);
            return memoryStream;
        }

        public async Task<MemoryStream> RepositorySignPackageStreamAsync(
            Stream inputPackageStream,
            X509Certificate2 signingCertificate,
            ITestOutputHelper output)
        {
            var timestampUri = await GetTimestampServiceUrlAsync();

            return await RepositorySignPackageStreamAsync(
                inputPackageStream,
                signingCertificate,
                timestampUri,
                output);
        }

        public async Task<MemoryStream> RepositorySignPackageStreamAsync(
            Stream inputPackageStream,
            X509Certificate2 certificate,
            Uri timestampUri,
            ITestOutputHelper output)
        {
            var packageBytes = await GenerateSignedPackageBytesAsync(
                inputPackageStream,
                new RepositorySignPackageRequest(
                    certificate,
                    HashAlgorithmName.SHA256,
                    HashAlgorithmName.SHA256,
                    new Uri(TestResources.V3ServiceIndexUrl),
                    new[] { "nuget", "microsoft" }),
                timestampUri,
                output);

            var memoryStream = new MemoryStream();
            await memoryStream.WriteAsync(packageBytes, 0, packageBytes.Length);
            return memoryStream;
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
