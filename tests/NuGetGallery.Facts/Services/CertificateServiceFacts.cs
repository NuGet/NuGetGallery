// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using Xunit;

namespace NuGetGallery.Services
{
    public class CertificateServiceFacts
    {
        private static readonly byte[] _certificateBytes = Encoding.UTF8.GetBytes("certificate");
        private const string _sha1Thumbprint = "735ad571c189d7ba84464bf4a9f1d2280175b128";
        private const string _sha256Thumbprint = "03d66dd08835c1ca3f128cceacd1f31ac94163096b20f445ae84285bc0832d72";

        private readonly Mock<IAuditingService> _auditingService;
        private readonly Mock<IEntityRepository<Certificate>> _certificateRepository;
        private readonly Mock<ICertificateValidator> _certificateValidator;
        private readonly Mock<IEntitiesContext> _entitiesContext;
        private readonly Mock<IFileStorageService> _fileStorageService;
        private readonly Mock<ITelemetryService> _telemetryService;
        private readonly Mock<IEntityRepository<User>> _userRepository;

        private readonly User _user;
        private readonly Certificate _certificate;

        public CertificateServiceFacts()
        {
            _auditingService = new Mock<IAuditingService>(MockBehavior.Strict);
            _certificateRepository = new Mock<IEntityRepository<Certificate>>(MockBehavior.Strict);
            _certificateValidator = new Mock<ICertificateValidator>(MockBehavior.Strict);
            _entitiesContext = new Mock<IEntitiesContext>(MockBehavior.Strict);
            _fileStorageService = new Mock<IFileStorageService>(MockBehavior.Strict);
            _telemetryService = new Mock<ITelemetryService>(MockBehavior.Strict);
            _userRepository = new Mock<IEntityRepository<User>>(MockBehavior.Strict);

            _user = new User()
            {
                Key = 3,
                Username = "_user"
            };
            _certificate = new Certificate()
            {
                Key = 1,
#pragma warning disable CS0618 // Set the value for minimal testing.
                Sha1Thumbprint = _sha1Thumbprint,
#pragma warning restore CS0618
                Thumbprint = _sha256Thumbprint,
                UserCertificates = new List<UserCertificate>()
            };
        }

        [Fact]
        public void Constructor_WhenCertificateValidatorIsNull_Throws()
        {
            ICertificateValidator certificateValidator = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new CertificateService(
                    certificateValidator,
                    _certificateRepository.Object,
                    _userRepository.Object,
                    _entitiesContext.Object,
                    _fileStorageService.Object,
                    _auditingService.Object,
                    _telemetryService.Object));

            Assert.Equal("certificateValidator", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenCertificateRepositoryIsNull_Throws()
        {
            IEntityRepository<Certificate> certificateRepository = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new CertificateService(
                    _certificateValidator.Object,
                    certificateRepository,
                    _userRepository.Object,
                    _entitiesContext.Object,
                    _fileStorageService.Object,
                    _auditingService.Object,
                    _telemetryService.Object));

            Assert.Equal("certificateRepository", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenUserRepositoryIsNull_Throws()
        {
            IEntityRepository<User> userRepository = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new CertificateService(
                    _certificateValidator.Object,
                    _certificateRepository.Object,
                    userRepository,
                    _entitiesContext.Object,
                    _fileStorageService.Object,
                    _auditingService.Object,
                    _telemetryService.Object));

            Assert.Equal("userRepository", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenEntitiesContextIsNull_Throws()
        {
            IEntitiesContext entitiesContext = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new CertificateService(
                    _certificateValidator.Object,
                    _certificateRepository.Object,
                    _userRepository.Object,
                    entitiesContext,
                    _fileStorageService.Object,
                    _auditingService.Object,
                    _telemetryService.Object));

            Assert.Equal("entitiesContext", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenFileStorageServiceIsNull_Throws()
        {
            IFileStorageService fileStorageService = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new CertificateService(
                    _certificateValidator.Object,
                    _certificateRepository.Object,
                    _userRepository.Object,
                    _entitiesContext.Object,
                    fileStorageService,
                    _auditingService.Object,
                    _telemetryService.Object));

            Assert.Equal("fileStorageService", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenAuditingServiceIsNull_Throws()
        {
            IAuditingService auditingService = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new CertificateService(
                    _certificateValidator.Object,
                    _certificateRepository.Object,
                    _userRepository.Object,
                    _entitiesContext.Object,
                    _fileStorageService.Object,
                    auditingService,
                    _telemetryService.Object));

            Assert.Equal("auditingService", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenTelemetryServiceIsNull_Throws()
        {
            ITelemetryService telemetryService = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new CertificateService(
                    _certificateValidator.Object,
                    _certificateRepository.Object,
                    _userRepository.Object,
                    _entitiesContext.Object,
                    _fileStorageService.Object,
                    _auditingService.Object,
                    telemetryService));

            Assert.Equal("telemetryService", exception.ParamName);
        }

        [Fact]
        public async Task AddCertificateAsync_WhenFileIsNull_Throws()
        {
            var service = GetCertificateService();

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => service.AddCertificateAsync(file: null));

            Assert.Equal("file", exception.ParamName);
        }

        [Fact]
        public async Task AddCertificateAsync_WhenCertificateDoesNotAlreadyExistInEntitiesContext_Succeeds()
        {
            _certificateValidator.Setup(x => x.Validate(It.IsNotNull<HttpPostedFileBase>()));
            _certificateRepository.Setup(x => x.GetAll())
                .Returns(new EnumerableQuery<Certificate>(Array.Empty<Certificate>()));
            _certificateRepository.Setup(
                x => x.InsertOnCommit(
                    It.Is<Certificate>(certificate => certificate.Thumbprint == _sha256Thumbprint)));
            _certificateRepository.Setup(x => x.CommitChangesAsync())
                .Returns(Task.CompletedTask);
            _fileStorageService.Setup(
                x => x.SaveFileAsync(
                    It.Is<string>(folderName => folderName == CoreConstants.Folders.UserCertificatesFolderName),
                    It.Is<string>(fileName => fileName == $"SHA-256/{_sha256Thumbprint}.cer"),
                    It.IsNotNull<Stream>(),
                    It.Is<bool>(overwrite => overwrite == false)))
                .Returns(Task.CompletedTask);
            _auditingService.Setup(
                x => x.SaveAuditRecordAsync(
                    It.Is<CertificateAuditRecord>(record =>
                        record.Action == AuditedCertificateAction.Add && record.Thumbprint == _sha256Thumbprint)))
                .Returns(Task.CompletedTask);
            _telemetryService.Setup(
                x => x.TrackCertificateAdded(It.Is<string>(thumbprint => thumbprint == _sha256Thumbprint)));

            var service = GetCertificateService();

            var file = GetFile();

            using (file.InputStream)
            {
                var certificate = await service.AddCertificateAsync(file);

                Assert.NotNull(certificate);
#pragma warning disable CS0618 // Assert the value is set properly, but we will never read it.
                Assert.Equal(_sha1Thumbprint, certificate.Sha1Thumbprint);
#pragma warning restore CS0618
                Assert.Equal(_sha256Thumbprint, certificate.Thumbprint);
            }

            VerifyMockExpectations();
        }

        [Fact]
        public async Task AddCertificateAsync_WhenCertificateAlreadyExistsInEntitiesContext_Succeeds()
        {
            _certificateValidator.Setup(x => x.Validate(It.IsNotNull<HttpPostedFileBase>()));
            _certificateRepository.Setup(x => x.GetAll())
                .Returns(new EnumerableQuery<Certificate>(new[] { _certificate }));

            var service = GetCertificateService();

            var file = GetFile();

            using (file.InputStream)
            {
                var certificate = await service.AddCertificateAsync(file);

                Assert.NotNull(certificate);
                Assert.Equal(1, certificate.Key);
                Assert.Equal(_sha256Thumbprint, certificate.Thumbprint);
            }

            VerifyMockExpectations();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task ActivateCertificateAsync_WhenThumbprintIsInvalid_Throws(string thumbprint)
        {
            var service = GetCertificateService();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => service.ActivateCertificateAsync(thumbprint, new User()));

            Assert.Equal("thumbprint", exception.ParamName);
        }

        [Fact]
        public async Task ActivateCertificateAsync_WhenAccountIsNull_Throws()
        {
            var service = GetCertificateService();

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => service.ActivateCertificateAsync(_sha256Thumbprint, account: null));

            Assert.Equal("account", exception.ParamName);
        }

        [Fact]
        public async Task ActivateCertificateAsync_WhenCertificateDoesNotAlreadyExistInEntitiesContext_Throws()
        {
            _certificateRepository.Setup(x => x.GetAll())
                .Returns(new EnumerableQuery<Certificate>(Array.Empty<Certificate>()));

            var service = GetCertificateService();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => service.ActivateCertificateAsync(_sha256Thumbprint, new User()));

            Assert.Equal("thumbprint", exception.ParamName);
            Assert.StartsWith("The certificate does not exist.", exception.Message);

            VerifyMockExpectations();
        }

        [Fact]
        public async Task ActivateCertificateAsync_WhenCertificateAlreadyExistsInEntitiesContextButUserCertificateDoesNot_Succeeds()
        {
            _certificateRepository.Setup(x => x.GetAll())
                .Returns(new EnumerableQuery<Certificate>(new[] { _certificate }));

            var userCertificates = new Mock<DbSet<UserCertificate>>(MockBehavior.Strict);
            var userCertificate = new UserCertificate()
            {
                Key = 7,
                CertificateKey = _certificate.Key,
                Certificate = _certificate,
                UserKey = _user.Key,
                User = _user
            };
            userCertificates.Setup(x => x.Add(It.Is<UserCertificate>(uc => uc.CertificateKey == _certificate.Key && uc.UserKey == _user.Key)))
                .Returns(userCertificate);
            _entitiesContext.SetupGet(x => x.UserCertificates)
                .Returns(userCertificates.Object);
            _entitiesContext.Setup(x => x.SaveChangesAsync())
                .Returns(Task.FromResult(0))
                .Callback(() => _certificate.UserCertificates.Add(userCertificate));
            _auditingService.Setup(x => x.SaveAuditRecordAsync(
                It.Is<CertificateAuditRecord>(
                    record => record.Action == AuditedCertificateAction.Activate &&
                        record.HashAlgorithm == "SHA-256" &&
                        record.Thumbprint == _sha256Thumbprint)))
                .Returns(Task.CompletedTask);
            _telemetryService.Setup(x => x.TrackCertificateActivated(
                It.Is<string>(thumbprint => thumbprint == _sha256Thumbprint)));

            var service = GetCertificateService();

            await service.ActivateCertificateAsync(_sha256Thumbprint, _user);

            userCertificate = _certificate.UserCertificates.Single();

            Assert.Same(_certificate, userCertificate.Certificate);
            Assert.Same(_user, userCertificate.User);

            VerifyMockExpectations();

            userCertificates.VerifyAll();
        }

        [Fact]
        public async Task ActivateCertificateAsync_WhenCertificateAndUserCertificateAlreadyExistInEntitiesContext_Succeeds()
        {
            _certificate.UserCertificates.Add(new UserCertificate()
            {
                Key = 7,
                UserKey = _user.Key,
                User = _user,
                CertificateKey = _certificate.Key,
                Certificate = _certificate
            });

            _certificateRepository.Setup(x => x.GetAll())
                .Returns(new EnumerableQuery<Certificate>(new[] { _certificate }));

            var service = GetCertificateService();

            await service.ActivateCertificateAsync(_sha256Thumbprint, _user);

            var userCertificate = _certificate.UserCertificates.Single();

            Assert.Same(_certificate, userCertificate.Certificate);
            Assert.Same(_user, userCertificate.User);

            VerifyMockExpectations();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task DeactivateCertificateAsync_WhenThumbprintIsInvalid_Throws(string thumbprint)
        {
            var service = GetCertificateService();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => service.DeactivateCertificateAsync(thumbprint, new User()));

            Assert.Equal("thumbprint", exception.ParamName);
        }

        [Fact]
        public async Task DeativateCertificateAsync_WhenAccountIsNull_Throws()
        {
            var service = GetCertificateService();

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => service.DeactivateCertificateAsync(_sha256Thumbprint, account: null));

            Assert.Equal("account", exception.ParamName);
        }

        [Fact]
        public async Task DeativateCertificateAsync_WhenCertificateDoesNotAlreadyExistInEntitiesContext_Throws()
        {
            _certificateRepository.Setup(x => x.GetAll())
                .Returns(new EnumerableQuery<Certificate>(Array.Empty<Certificate>()));

            var service = GetCertificateService();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => service.DeactivateCertificateAsync(_sha256Thumbprint, new User()));

            Assert.Equal("thumbprint", exception.ParamName);
            Assert.StartsWith("The certificate does not exist.", exception.Message);

            VerifyMockExpectations();
        }

        [Fact]
        public async Task DeactivateCertificateAsync_WhenCertificateAlreadyExistsInEntitiesContextButUserCertificateDoesNot_Succeeds()
        {
            _certificateRepository.Setup(x => x.GetAll())
                .Returns(new EnumerableQuery<Certificate>(new[] { _certificate }));

            var service = GetCertificateService();

            await service.DeactivateCertificateAsync(_sha256Thumbprint, _user);

            Assert.Empty(_certificate.UserCertificates);

            VerifyMockExpectations();
        }

        [Fact]
        public async Task DeativateCertificateAsync_WhenCertificateAndUserCertificateAlreadyExistInEntitiesContextAndUserCertificate_Succeeds()
        {
            _certificate.UserCertificates.Add(new UserCertificate()
            {
                Key = 7,
                UserKey = _user.Key,
                User = _user,
                CertificateKey = _certificate.Key,
                Certificate = _certificate
            });

            _certificateRepository.Setup(x => x.GetAll())
                .Returns(new EnumerableQuery<Certificate>(new[] { _certificate }));
            _entitiesContext.Setup(x => x.DeleteOnCommit(
                It.Is<UserCertificate>(uc => uc.CertificateKey == _certificate.Key && uc.UserKey == _user.Key)));
            _entitiesContext.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(0)
                .Callback(() =>
                {
                    _certificate.UserCertificates.Clear();
                });
            _auditingService.Setup(x => x.SaveAuditRecordAsync(
                It.Is<CertificateAuditRecord>(
                    record => record.Action == AuditedCertificateAction.Deactivate &&
                        record.HashAlgorithm == "SHA-256" &&
                        record.Thumbprint == _sha256Thumbprint)))
                .Returns(Task.CompletedTask);
            _telemetryService.Setup(x => x.TrackCertificateDeactivated(
                It.Is<string>(thumbprint => thumbprint == _sha256Thumbprint)));

            var service = GetCertificateService();

            await service.DeactivateCertificateAsync(_sha256Thumbprint, _user);

            var userCertificate = _certificate.UserCertificates.SingleOrDefault();

            Assert.Null(userCertificate);

            VerifyMockExpectations();
        }

        [Fact]
        public void GetCertificates_WhenAccountIsNull_Throws()
        {
            var service = GetCertificateService();

            var exception = Assert.Throws<ArgumentNullException>(
                () => service.GetCertificates(account: null));

            Assert.Equal("account", exception.ParamName);
        }

        [Fact]
        public void GetCertificates_WhenNoUserCertificatesExistInEntitiesContext_ReturnsEmptyEnumerable()
        {
            _userRepository.Setup(x => x.GetAll())
                .Returns(new EnumerableQuery<User>(new[] { _user }));

            var service = GetCertificateService();

            var certificates = service.GetCertificates(_user);

            Assert.Empty(certificates);

            VerifyMockExpectations();
        }

        [Fact]
        public void GetCertificates_WhenUserCertificateExistsInEntitiesContext_ReturnsCertificate()
        {
            _user.UserCertificates.Add(new UserCertificate()
            {
                Key = 7,
                UserKey = _user.Key,
                User = _user,
                CertificateKey = _certificate.Key,
                Certificate = _certificate
            });

            _userRepository.Setup(x => x.GetAll())
                .Returns(new EnumerableQuery<User>(new[] { _user }));

            var service = GetCertificateService();

            var certificates = service.GetCertificates(_user);

            Assert.Single(certificates);
            Assert.Same(_certificate, certificates.Single());

            VerifyMockExpectations();
        }

        private StubHttpPostedFile GetFile()
        {
            var stream = new MemoryStream(_certificateBytes);

            return new StubHttpPostedFile((int)stream.Length, "certificate.cer", stream);
        }

        private CertificateService GetCertificateService()
        {
            return new CertificateService(
                _certificateValidator.Object,
                _certificateRepository.Object,
                _userRepository.Object,
                _entitiesContext.Object,
                _fileStorageService.Object,
                _auditingService.Object,
                _telemetryService.Object);
        }

        private void VerifyMockExpectations()
        {
            _certificateValidator.VerifyAll();
            _certificateRepository.VerifyAll();
            _userRepository.VerifyAll();
            _entitiesContext.VerifyAll();
            _fileStorageService.VerifyAll();
            _auditingService.VerifyAll();
            _telemetryService.VerifyAll();
        }
    }
}