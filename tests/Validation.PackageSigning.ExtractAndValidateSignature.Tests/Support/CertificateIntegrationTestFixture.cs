// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;
using Xunit.Abstractions;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    /// <summary>
    /// This test fixture trusts the root certificate of checked in signed packages. This handles adding and removing
    /// the root certificate from the local machine trusted roots. Any tests with this fixture require admin elevation.
    /// </summary>
    public class CertificateIntegrationTestFixture : IDisposable
    {
        private static readonly string _testTimestampServer = Environment.GetEnvironmentVariable("TIMESTAMP_SERVER_URL");

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private byte[] _signedPackageBytes1;

        public CertificateIntegrationTestFixture()
        {
            Assert.True(
                IsAdministrator(),
                "This test must be executing with administrator privileges since it installs a trusted root.");

            LeafCertificate1 = TestCertificate
                .Generate(SigningTestUtility.CertificateModificationGeneratorForCodeSigningEkuCert)
                .WithPrivateKeyAndTrust(StoreName.Root, StoreLocation.LocalMachine);
            LeafCertificate1Thumbprint = LeafCertificate1.TrustedCert.ComputeSHA256Thumbprint();
        }

        public TrustedTestCert<TestCertificate> LeafCertificate1 { get; }
        public string LeafCertificate1Thumbprint { get; }
        public Task<SignedPackageArchive> GetSignedPackage1Async(ITestOutputHelper output) => GetSignedPackageAsync(
            new Reference<byte[]>(
                () => _signedPackageBytes1,
                x => _signedPackageBytes1 = x),
            TestResources.SignedPackageLeaf1,
            LeafCertificate1,
            output);
        public Task<MemoryStream> GetSignedPackageStream1Async(ITestOutputHelper output) => GetSignedPackageStreamAsync(
            new Reference<byte[]>(
                () => _signedPackageBytes1,
                x => _signedPackageBytes1 = x),
            TestResources.SignedPackageLeaf1,
            LeafCertificate1,
            output);
        
        public void Dispose()
        {
            LeafCertificate1?.Dispose();
        }

        /// <summary>
        /// Source: https://stackoverflow.com/a/11660205
        /// </summary>
        private static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private async Task<MemoryStream> GetSignedPackageStreamAsync(
            Reference<byte[]> reference,
            string resourceName,
            TrustedTestCert<TestCertificate> certificate,
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
            TrustedTestCert<TestCertificate> certificate,
            ITestOutputHelper output)
        {
            return new SignedPackageArchive(
                await GetSignedPackageStreamAsync(reference, resourceName, certificate, output),
                new MemoryStream());
        }
        
        private static async Task<byte[]> GenerateSignedPackageBytesAsync(string resourceName, TrustedTestCert<TestCertificate> certificate, ITestOutputHelper output)
        {
            if (string.IsNullOrWhiteSpace(_testTimestampServer))
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(_testTimestampServer),
                    "You must set a TIMESTAMP_SERVER_URL environment variable to an accessible timestamping authority URL.");
            }

            var testLogger = new TestLogger(output);
            var timestampProvider = new Rfc3161TimestampProvider(new Uri(_testTimestampServer));
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
                    var request = new AuthorSignPackageRequest(certificate.TrustedCert, HashAlgorithmName.SHA256);
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
