// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Internal.NuGet.Testing.SignedPackages;
using NuGet.Common;
using TestUtil;
using Xunit;

namespace Validation.PackageSigning.Core.Tests.Support
{
    /// <summary>
    /// This test fixture trusts the root certificate of checked in signed packages. This handles adding and removing
    /// the root certificate from the local machine trusted roots. Any tests with this fixture require admin elevation.
    /// </summary>
    public class CertificateIntegrationTestFixture : IAsyncLifetime
    {
        private readonly AsyncLazy<SigningTestServer> _testServer;
        private readonly AsyncLazy<CertificateAuthority> _rootCertificateAuthority;
        private readonly AsyncLazy<CertificateAuthority> _certificateAuthority;
        private readonly AsyncLazy<TimestampService> _timestampService;
        private readonly AsyncLazy<Uri> _timestampServiceUrl;
        private readonly AsyncLazy<X509Certificate2> _signingCertificate;
        private readonly AsyncLazy<string> _signingCertificateThumbprint;
        private TrustedTestCert<X509Certificate2> _trustedRoot;
        private readonly DisposableList<IDisposable> _responders;
        private bool _testServerStarted = false;

        public CertificateIntegrationTestFixture()
        {
            Assert.True(
                UserHelper.IsAdministrator(),
                $"This test must be executing with administrator privileges since it installs a trusted root. Add {UserHelper.EnableSkipVariableName} environment variable to skip this test.");
            _testServer = new AsyncLazy<SigningTestServer>(SigningTestServer.CreateAsync);
            _rootCertificateAuthority = new AsyncLazy<CertificateAuthority>(CreateDefaultTrustedRootCertificateAuthorityAsync);
            _certificateAuthority = new AsyncLazy<CertificateAuthority>(CreateDefaultTrustedCertificateAuthorityAsync);
            _timestampService = new AsyncLazy<TimestampService>(CreateDefaultTrustedTimestampServiceAsync);
            _timestampServiceUrl = new AsyncLazy<Uri>(CreateDefaultTrustedTimestampServiceUrlAsync);
            _signingCertificate = new AsyncLazy<X509Certificate2>(CreateDefaultTrustedSigningCertificateAsync);
            _signingCertificateThumbprint = new AsyncLazy<string>(GetDefaultTrustedSigningCertificateThumbprintAsync);
            _responders = new DisposableList<IDisposable>();
        }

        public async Task<SigningTestServer> GetTestServerAsync()
        {
            _testServerStarted = true;
            return await _testServer;
        }

        public async Task<Uri> GetTimestampServiceUrlAsync() => await _timestampServiceUrl;

        public async Task<X509Certificate2> GetSigningCertificateAsync()
        {
            return new X509Certificate2(await _signingCertificate);
        }

        public async Task<string> GetSigningCertificateThumbprintAsync() => await _signingCertificateThumbprint;

        protected async Task<CertificateAuthority> GetRootCertificateAuthorityAsync() => await _rootCertificateAuthority;
        protected async Task<CertificateAuthority> GetCertificateAuthorityAsync() => await _certificateAuthority;
        protected DisposableList<IDisposable> GetResponders() => _responders;

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            _trustedRoot?.Dispose();
            _responders.Dispose();

            if (_testServerStarted)
            {
                (await _testServer).Dispose();
            }
        }

        private async Task<CertificateAuthority> CreateDefaultTrustedRootCertificateAuthorityAsync()
        {
            var testServer = await GetTestServerAsync();
            var rootCa = CertificateAuthority.Create(testServer.Url);
            var rootCertificate = new X509Certificate2(rootCa.Certificate);

            _trustedRoot = new TrustedTestCert<X509Certificate2>(
                rootCertificate,
                certificate => certificate,
                new[] { X509StorePurpose.CodeSigning, X509StorePurpose.Timestamping },
                StoreName.Root,
                StoreLocation.LocalMachine);

            _responders.AddRange(testServer.RegisterResponders(rootCa));

            return rootCa;
        }

        private async Task<CertificateAuthority> CreateDefaultTrustedCertificateAuthorityAsync()
        {
            var testServer = await GetTestServerAsync();
            var rootCa = await GetRootCertificateAuthorityAsync();
            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority();

            _responders.AddRange(testServer.RegisterResponders(intermediateCa));

            return intermediateCa;
        }

        private async Task<TimestampService> CreateDefaultTrustedTimestampServiceAsync()
        {
            var testServer = await GetTestServerAsync();
            var ca = await GetCertificateAuthorityAsync();
            var timestampService = TimestampService.Create(ca);

            _responders.Add(testServer.RegisterResponder(timestampService));

            return timestampService;
        }

        private async Task<Uri> CreateDefaultTrustedTimestampServiceUrlAsync()
        {
            var timestampService = await _timestampService;
            return timestampService.Url;
        }

        private async Task<X509Certificate2> CreateDefaultTrustedSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthorityAsync();
            return CreateSigningCertificate(ca);
        }

        public X509Certificate2 CreateSigningCertificate(CertificateAuthority ca)
        {
            void CustomizeAsSigningCertificate(CertificateRequest certificateRequest)
            {
                certificateRequest.AddSigningEku();
                certificateRequest.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            return IssueCertificate(ca, "Signing", CustomizeAsSigningCertificate).certificate;
        }

        public async Task<X509Certificate2> CreateUntrustedRootSigningCertificateAsync()
        {
            var options = IssueCertificateOptions.CreateDefaultForRootCertificateAuthority();

            options.CustomizeCertificate = (CertificateRequest certificateRequest) =>
            {
                certificateRequest.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey, critical: false));
                certificateRequest.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
                certificateRequest.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
                certificateRequest.AddSigningEku();
            };

            var testServer = await GetTestServerAsync();
            var rootCa = CertificateAuthority.Create(testServer.Url, options);

            return rootCa.Certificate;
        }

        public async Task<RevokableCertificate> CreateRevokableSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthorityAsync();

            void CustomizeAsSigningCertificate(CertificateRequest certificateRequest)
            {
                certificateRequest.AddSigningEku();
                certificateRequest.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            var issued = IssueCertificate(ca, "Revoked Signing", CustomizeAsSigningCertificate);
            var revocationDate = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(1));

            void Revoke()
            {
                ca.Revoke(issued.publicCertificate, X509RevocationReason.Unspecified, revocationDate);
            }

            Task WaitForResponseExpirationAsync()
            {
                return ca.OcspResponder.WaitForResponseExpirationAsync(issued.publicCertificate);
            }

            return new RevokableCertificate(
                issued.certificate,
                Revoke,
                WaitForResponseExpirationAsync);
        }

        public async Task<UntrustedSigningCertificate> CreateUntrustedSigningCertificateAsync()
        {
            var testServer = await GetTestServerAsync();
            var untrustedRootCa = CertificateAuthority.Create(testServer.Url);
            var untrustedRootCertificate = new X509Certificate2(untrustedRootCa.Certificate);
            var responders = testServer.RegisterRespondersForEntireChain(untrustedRootCa);

            var certificate = CreateSigningCertificate(untrustedRootCa);

            var disposable = new DisposableList<IDisposable> { untrustedRootCertificate, responders, certificate };

            return new UntrustedSigningCertificate(untrustedRootCertificate, certificate, disposable);
        }

        public async Task<X509Certificate2> CreateExpiringSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthorityAsync();

            void CustomizeExpiringSigningCertificate(CertificateRequest certificateRequest)
            {
                certificateRequest.AddSigningEku();
                certificateRequest.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddSeconds(-2);
            DateTimeOffset notAfter = DateTimeOffset.UtcNow.AddSeconds(10);

            var (@public, certificate) = IssueCertificate(ca, "Expired Signing", CustomizeExpiringSigningCertificate, notBefore, notAfter);

            return certificate;
        }

        public async Task<CustomTimestampService> CreateCustomTimestampServiceAsync(TimestampServiceOptions options)
        {
            var testServer = await GetTestServerAsync();
            var rootCa = await GetRootCertificateAuthorityAsync();
            var timestampService = TimestampService.Create(rootCa, options);
            var responders = testServer.RegisterDefaultResponders(timestampService);

            return new CustomTimestampService(
                timestampService.Url,
                responders);
        }

        public async Task<UntrustedTimestampService> CreateUntrustedTimestampServiceAsync()
        {
            var testServer = await GetTestServerAsync();
            var untrustedRootCa = CertificateAuthority.Create(testServer.Url);
            var untrustedRootCertificate = new X509Certificate2(untrustedRootCa.Certificate);
            var timestampService = TimestampService.Create(untrustedRootCa);
            var responders = testServer.RegisterDefaultResponders(timestampService);

            return new UntrustedTimestampService(
                untrustedRootCertificate,
                timestampService.Url,
                responders);
        }

        public async Task<RevokableTimestampService> CreateRevokableTimestampServiceAsync()
        {
            var testServer = await GetTestServerAsync();
            var rootCa = await GetRootCertificateAuthorityAsync();
            var timestampService = TimestampService.Create(rootCa);
            var responders = testServer.RegisterDefaultResponders(timestampService);

            var revocationDate = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(1));

            void Revoke()
            {
                rootCa.Revoke(timestampService.Certificate, X509RevocationReason.Unspecified, revocationDate);
            }

            Task WaitForResponseExpirationAsync()
            {
                return rootCa.OcspResponder.WaitForResponseExpirationAsync(timestampService.Certificate);
            }

            return new RevokableTimestampService(
                timestampService.Url,
                Revoke,
                WaitForResponseExpirationAsync,
                responders);
        }

        public async Task<SigningCertificateWithUnavailableRevocation> CreateSigningCertificateWithUnavailableRevocationAsync()
        {
            var testServer = await GetTestServerAsync();
            var rootCa = await GetRootCertificateAuthorityAsync();
            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority();

            var intermediateCaResponder = testServer.RegisterResponder(intermediateCa);

            IDisposable AddOcspResponder()
            {
                return testServer.RegisterResponder(intermediateCa.OcspResponder);
            }

            void CustomizeAsSigningCertificate(CertificateRequest certificateRequest)
            {
                certificateRequest.AddSigningEku();
                certificateRequest.AddAuthorityInfoAccess(intermediateCa, addOcsp: true, addCAIssuers: true);
            }

            var issued = IssueCertificate(intermediateCa, "Signing Certificate With Unavailable Revocation", CustomizeAsSigningCertificate);

            Task WaitForResponseExpirationAsync()
            {
                return intermediateCa.OcspResponder.WaitForResponseExpirationAsync(issued.publicCertificate);
            }

            return new SigningCertificateWithUnavailableRevocation(
                issued.certificate,
                AddOcspResponder,
                WaitForResponseExpirationAsync,
                intermediateCaResponder);
        }

        public async Task<TimestampServiceWithUnavailableRevocation> CreateTimestampServiceWithUnavailableRevocationAsync()
        {
            var testServer = await GetTestServerAsync();
            var rootCa = CertificateAuthority.Create(testServer.Url);
            var rootCertificate = new X509Certificate2(rootCa.Certificate);

            var trust = new TrustedTestCert<X509Certificate2>(
                rootCertificate,
                certificate => certificate,
                new[] { X509StorePurpose.CodeSigning, X509StorePurpose.Timestamping },
                StoreName.Root,
                StoreLocation.LocalMachine);

            var timestampService = TimestampService.Create(rootCa);

            // Do not add `rootCertificate`, because its disposal will cause subsequent disposal
            // of `trust` to fail and trust removal to fail.
            // Disposing `trust` already disposes `rootCertificate`.
            var disposable = new DisposableList<IDisposable> { trust };

            Task WaitForResponseExpirationAsync()
            {
                return rootCa.OcspResponder.WaitForResponseExpirationAsync(timestampService.Certificate);
            }

            return new TimestampServiceWithUnavailableRevocation(
                testServer,
                timestampService,
                WaitForResponseExpirationAsync,
                disposable);
        }

        protected (X509Certificate2 publicCertificate, X509Certificate2 certificate) IssueCertificate(
            CertificateAuthority ca,
            string name,
            Action<CertificateRequest> customizeCertificate,
            DateTimeOffset? notBefore = null,
            DateTimeOffset? notAfter = null)
        {
            using (RSA keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048))
            {
                notBefore ??= DateTimeOffset.UtcNow.AddSeconds(-10);
                notAfter ??= DateTimeOffset.UtcNow.AddMinutes(10);

                X509Certificate2 certificate = ca.IssueCertificate(new IssueCertificateOptions
                {
                    CustomizeCertificate = customizeCertificate,
                    NotAfter = notAfter.Value,
                    NotBefore = notBefore.Value,
                    KeyPair = keyPair,
                    SubjectName = new X500DistinguishedName($"C=US,ST=WA,L=Redmond,O=NuGet,CN=NuGet Test ${name} Certificate ({Guid.NewGuid()})")
                });

                X509Certificate2 publicCertificate = new(certificate.RawData);

                return (publicCertificate, certificate);
            }
        }

        private async Task<string> GetDefaultTrustedSigningCertificateThumbprintAsync()
        {
            var certificate = await GetSigningCertificateAsync();
            return certificate.ComputeSHA256Thumbprint();
        }

        public class RevokableCertificate
        {
            private readonly Action _revokeAction;
            private readonly Func<Task> _waitForResponseExpiration;

            public RevokableCertificate(X509Certificate2 certificate, Action revokeAction, Func<Task> waitForResponseExpiration)
            {
                Certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
                _revokeAction = revokeAction ?? throw new ArgumentNullException(nameof(revokeAction));
                _waitForResponseExpiration = waitForResponseExpiration ?? throw new ArgumentNullException(nameof(waitForResponseExpiration));
            }

            public X509Certificate2 Certificate { get; }

            public void Revoke() => _revokeAction();
            public Task WaitForResponseExpirationAsync() => _waitForResponseExpiration();
        }

        public class UntrustedSigningCertificate : IDisposable
        {
            private readonly X509Certificate2 _rootCertificate;
            private readonly IDisposable _disposable;

            public UntrustedSigningCertificate(X509Certificate2 rootCertificate, X509Certificate2 signingCertificate, IDisposable disposable)
            {
                _rootCertificate = rootCertificate ?? throw new ArgumentNullException(nameof(rootCertificate));
                Certificate = signingCertificate ?? throw new ArgumentNullException(nameof(Certificate));
                _disposable = disposable;
            }

            public X509Certificate2 Certificate { get; }

            public IDisposable Trust()
            {
                return new TrustedTestCert<X509Certificate2>(
                    _rootCertificate,
                    x => x,
                    new[] { X509StorePurpose.CodeSigning, X509StorePurpose.Timestamping },
                    StoreName.Root,
                    StoreLocation.LocalMachine);
            }

            public void Dispose() => _disposable?.Dispose();
        }

        public class SigningCertificateWithUnavailableRevocation : IDisposable
        {
            private readonly Func<IDisposable> _respondToRevocations;
            private readonly Func<Task> _waitForResponseExpiration;
            private readonly IDisposable _disposable;

            public SigningCertificateWithUnavailableRevocation(
                X509Certificate2 certificate,
                Func<IDisposable> respondToRevocations,
                Func<Task> waitForResponseExpiration,
                IDisposable disposable)
            {
                Certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
                _respondToRevocations = respondToRevocations ?? throw new ArgumentNullException(nameof(respondToRevocations));
                _waitForResponseExpiration = waitForResponseExpiration ?? throw new ArgumentNullException(nameof(waitForResponseExpiration));
                _disposable = disposable;
            }

            public X509Certificate2 Certificate { get; }

            public IDisposable RespondToRevocations() => _respondToRevocations();
            public Task WaitForResponseExpirationAsync() => _waitForResponseExpiration();
            public void Dispose() => _disposable?.Dispose();
        }

        public class CustomTimestampService : IDisposable
        {
            private readonly IDisposable _disposable;

            public CustomTimestampService(Uri timestampServiceUrl, IDisposable disposable)
            {
                _disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
                Url = timestampServiceUrl ?? throw new ArgumentNullException(nameof(timestampServiceUrl));
            }

            public Uri Url { get; }

            public void Dispose() => _disposable.Dispose();
        }

        public class UntrustedTimestampService : IDisposable
        {
            private readonly X509Certificate2 _certificate;
            private readonly IDisposable _disposable;

            public UntrustedTimestampService(X509Certificate2 certificate, Uri timestampServiceUrl, IDisposable disposable)
            {
                _disposable = disposable;
                _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
                Url = timestampServiceUrl ?? throw new ArgumentNullException(nameof(timestampServiceUrl));
            }

            public Uri Url { get; }

            public IDisposable Trust()
            {
                return new TrustedTestCert<X509Certificate2>(
                    _certificate,
                    x => x,
                    new[] { X509StorePurpose.CodeSigning, X509StorePurpose.Timestamping },
                    StoreName.Root,
                    StoreLocation.LocalMachine);
            }

            public void Dispose() => _disposable?.Dispose();
        }

        public class RevokableTimestampService : IDisposable
        {
            private readonly Action _revokeAction;
            private readonly Func<Task> _waitForResponseExpiration;
            private readonly IDisposable _disposable;

            public RevokableTimestampService(Uri timestampServiceUrl, Action revokeAction, Func<Task> waitForResponseExpiration, IDisposable disposable)
            {
                _disposable = disposable;
                _revokeAction = revokeAction ?? throw new ArgumentNullException(nameof(revokeAction));
                _waitForResponseExpiration = waitForResponseExpiration ?? throw new ArgumentNullException(nameof(waitForResponseExpiration));

                Url = timestampServiceUrl ?? throw new ArgumentNullException(nameof(timestampServiceUrl));
            }

            public Uri Url { get; }

            public void Revoke() => _revokeAction();
            public Task WaitForResponseExpirationAsync() => _waitForResponseExpiration();

            public void Dispose() => _disposable?.Dispose();
        }

        public class TimestampServiceWithUnavailableRevocation : IDisposable
        {
            private readonly SigningTestServer _testServer;
            private readonly TimestampService _timestampService;
            private readonly Func<Task> _waitforResponseExpirationFunc;
            private readonly IDisposable _disposable;

            public TimestampServiceWithUnavailableRevocation(
                SigningTestServer testServer,
                TimestampService timestampService,
                Func<Task> waitforResponseExpirationFunc,
                IDisposable disposable)
            {
                _testServer = testServer ?? throw new ArgumentNullException(nameof(testServer));
                _timestampService = timestampService ?? throw new ArgumentNullException(nameof(timestampService));
                _waitforResponseExpirationFunc = waitforResponseExpirationFunc ?? throw new ArgumentNullException(nameof(waitforResponseExpirationFunc));
                _disposable = disposable;
            }

            public Uri Url => _timestampService.Url;

            public IDisposable RegisterDefaultResponders() => _testServer.RegisterDefaultResponders(_timestampService);
            public IDisposable RegisterResponders(bool addCa = true, bool addOcsp = true, bool addTimestamper = true)
                => _testServer.RegisterRespondersForTimestampServiceAndEntireChain(_timestampService, addCa, addOcsp, addTimestamper);

            public Task WaitForResponseExpirationAsync() => _waitforResponseExpirationFunc();

            public void Dispose() => _disposable?.Dispose();
        }
    }
}
