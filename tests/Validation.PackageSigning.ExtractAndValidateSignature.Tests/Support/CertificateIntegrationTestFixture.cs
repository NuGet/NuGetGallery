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
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Test.Utility.Signing;
using Validation.PackageSigning.ExtractAndValidateSignature.Tests.Support;
using Xunit;
using Xunit.Abstractions;
using GeneralName = Org.BouncyCastle.Asn1.X509.GeneralName;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    /// <summary>
    /// This test fixture trusts the root certificate of checked in signed packages. This handles adding and removing
    /// the root certificate from the local machine trusted roots. Any tests with this fixture require admin elevation.
    /// </summary>
    public class CertificateIntegrationTestFixture : IDisposable
    {
        private readonly Lazy<Task<SigningTestServer>> _testServer;
        private readonly Lazy<Task<CertificateAuthority>> _certificateAuthority;
        private readonly Lazy<Task<TimestampService>> _timestampService;
        private readonly Lazy<Task<Uri>> _timestampServiceUrl;
        private readonly Lazy<Task<X509Certificate2>> _signingCertificate;
        private readonly Lazy<Task<string>> _signingCertificateThumbprint;
        private TrustedTestCert<X509Certificate2> _trustedRoot;
        private readonly DisposableList _responders;

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private byte[] _signedPackageBytes1;

        public CertificateIntegrationTestFixture()
        {
            Assert.True(
                IsAdministrator(),
                "This test must be executing with administrator privileges since it installs a trusted root.");

            _testServer = new Lazy<Task<SigningTestServer>>(SigningTestServer.CreateAsync);
            _certificateAuthority = new Lazy<Task<CertificateAuthority>>(CreateDefaultTrustedCertificateAuthorityAsync);
            _timestampService = new Lazy<Task<TimestampService>>(CreateDefaultTrustedTimestampServiceAsync);
            _timestampServiceUrl = new Lazy<Task<Uri>>(CreateDefaultTrustedTimestampServiceUrlAsync);
            _signingCertificate = new Lazy<Task<X509Certificate2>>(CreateDefaultTrustedSigningCertificateAsync);
            _signingCertificateThumbprint = new Lazy<Task<string>>(GetDefaultTrustedSigningCertificateThumbprintAsync);
            _responders = new DisposableList();
        }

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

        public Task<SigningTestServer> GetTestServerAsync() => _testServer.Value;
        public Task<Uri> GetTimestampServiceUrlAsync() => _timestampServiceUrl.Value;
        public Task<X509Certificate2> GetSigningCertificateAsync() => _signingCertificate.Value;
        public Task<string> GetSigningCertificateThumbprintAsync() => _signingCertificateThumbprint.Value;

        public void Dispose()
        {
            _trustedRoot?.Dispose();
            _responders.Dispose();

            if (_testServer.IsValueCreated)
            {
                _testServer.Value.Result.Dispose();
            }
        }

        private async Task<CertificateAuthority> CreateDefaultTrustedCertificateAuthorityAsync()
        {
            var testServer = await GetTestServerAsync();
            var rootCa = CertificateAuthority.Create(testServer.Url);
            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority();
            var rootCertificate = new X509Certificate2(rootCa.Certificate.GetEncoded());

            _trustedRoot = new TrustedTestCert<X509Certificate2>(
                rootCertificate,
                certificate => certificate,
                StoreName.Root,
                StoreLocation.LocalMachine);

            _responders.AddRange(testServer.RegisterResponders(intermediateCa));

            return intermediateCa;
        }

        private async Task<TimestampService> CreateDefaultTrustedTimestampServiceAsync()
        {
            var testServer = await GetTestServerAsync();
            var ca = await _certificateAuthority.Value;
            var timestampService = TimestampService.Create(ca);

            _responders.Add(testServer.RegisterResponder(timestampService));

            return timestampService;
        }

        private async Task<Uri> CreateDefaultTrustedTimestampServiceUrlAsync()
        {
            var timestampService = await _timestampService.Value;
            return timestampService.Url;
        }

        private async Task<X509Certificate2> CreateDefaultTrustedSigningCertificateAsync()
        {
            var ca = await _certificateAuthority.Value;
            return CreateSigningCertificate(ca);
        }

        public X509Certificate2 CreateSigningCertificate(CertificateAuthority ca)
        {
            var keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);
            var publicCertificate = ca.IssueCertificate(
                keyPair.Public,
                new X509Name($"C=US,ST=WA,L=Redmond,O=NuGet,CN=NuGet Test Signing Certificate ({Guid.NewGuid()})"),
                generator =>
                {
                    SigningTestUtility.CertificateModificationGeneratorForCodeSigningEkuCert(generator);

                    generator.AddExtension(
                        X509Extensions.AuthorityInfoAccess,
                        critical: false,
                        extensionValue: new DerSequence(
                            new AccessDescription(AccessDescription.IdADOcsp,
                                new GeneralName(GeneralName.UniformResourceIdentifier, ca.OcspResponderUri.OriginalString)),
                            new AccessDescription(AccessDescription.IdADCAIssuers,
                                new GeneralName(GeneralName.UniformResourceIdentifier, ca.CertificateUri.OriginalString))));
                },
                notBefore: DateTime.UtcNow.AddSeconds(-10));

            var certificate = new X509Certificate2(publicCertificate.GetEncoded());
            certificate.PrivateKey = DotNetUtilities.ToRSA(keyPair.Private as RsaPrivateCrtKeyParameters);

            return certificate;
        }

        private async Task<string> GetDefaultTrustedSigningCertificateThumbprintAsync()
        {
            var certificate = await GetSigningCertificateAsync();
            return certificate.ComputeSHA256Thumbprint();
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
