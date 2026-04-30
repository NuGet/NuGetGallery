// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using Xunit;

namespace NuGetGallery.Services
{
    public class LockPackageServiceFacts
    {
        private readonly Mock<IEntityRepository<PackageRegistration>> _packageRegistrationRepository;
        private readonly Mock<IAuditingService> _auditingService;
        private readonly LockPackageService _service;

        public LockPackageServiceFacts()
        {
            _packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
            _auditingService = new Mock<IAuditingService>();
            _service = new LockPackageService(_packageRegistrationRepository.Object, _auditingService.Object);
        }

        [Fact]
        public async Task ReturnsPackageNotFoundWhenRegistrationDoesNotExist()
        {
            _packageRegistrationRepository
                .Setup(r => r.GetAll())
                .Returns(new List<PackageRegistration>().AsQueryable());

            var result = await _service.SetLockStateAsync("NonExistent.Package", true);

            Assert.Equal(LockPackageServiceResult.PackageNotFound, result);
        }

        [Fact]
        public async Task SetsIsLockedToTrueWhenLocking()
        {
            var registration = new PackageRegistration { Id = "My.Package", IsLocked = false };
            _packageRegistrationRepository
                .Setup(r => r.GetAll())
                .Returns(new List<PackageRegistration> { registration }.AsQueryable());

            var result = await _service.SetLockStateAsync("My.Package", true);

            Assert.Equal(LockPackageServiceResult.Success, result);
            Assert.True(registration.IsLocked);
        }

        [Fact]
        public async Task SetsIsLockedToFalseWhenUnlocking()
        {
            var registration = new PackageRegistration { Id = "My.Package", IsLocked = true };
            _packageRegistrationRepository
                .Setup(r => r.GetAll())
                .Returns(new List<PackageRegistration> { registration }.AsQueryable());

            var result = await _service.SetLockStateAsync("My.Package", false);

            Assert.Equal(LockPackageServiceResult.Success, result);
            Assert.False(registration.IsLocked);
        }

        [Fact]
        public async Task CommitsChangesWhenStateChanges()
        {
            var registration = new PackageRegistration { Id = "My.Package", IsLocked = false };
            _packageRegistrationRepository
                .Setup(r => r.GetAll())
                .Returns(new List<PackageRegistration> { registration }.AsQueryable());

            await _service.SetLockStateAsync("My.Package", true);

            _packageRegistrationRepository.Verify(r => r.CommitChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task WritesAuditRecordWithReasonAndCallerIdentity()
        {
            var registration = new PackageRegistration { Id = "My.Package", IsLocked = false };
            _packageRegistrationRepository
                .Setup(r => r.GetAll())
                .Returns(new List<PackageRegistration> { registration }.AsQueryable());

            await _service.SetLockStateAsync("My.Package", true, "security incident", "test-caller");

            _auditingService.Verify(
                a => a.SaveAuditRecordAsync(It.Is<PackageRegistrationAuditRecord>(r =>
                    r.Reason == "security incident" &&
                    r.CallerIdentity == "test-caller")),
                Times.Once);
        }

        [Fact]
        public async Task DoesNotAuditOrCommitWhenAlreadyLocked()
        {
            var registration = new PackageRegistration { Id = "My.Package", IsLocked = true };
            _packageRegistrationRepository
                .Setup(r => r.GetAll())
                .Returns(new List<PackageRegistration> { registration }.AsQueryable());

            var result = await _service.SetLockStateAsync("My.Package", true);

            Assert.Equal(LockPackageServiceResult.Success, result);
            _auditingService.Verify(
                a => a.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), Times.Never);
            _packageRegistrationRepository.Verify(
                r => r.CommitChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task DoesNotAuditOrCommitWhenAlreadyUnlocked()
        {
            var registration = new PackageRegistration { Id = "My.Package", IsLocked = false };
            _packageRegistrationRepository
                .Setup(r => r.GetAll())
                .Returns(new List<PackageRegistration> { registration }.AsQueryable());

            var result = await _service.SetLockStateAsync("My.Package", false);

            Assert.Equal(LockPackageServiceResult.Success, result);
            _auditingService.Verify(
                a => a.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), Times.Never);
            _packageRegistrationRepository.Verify(
                r => r.CommitChangesAsync(), Times.Never);
        }
    }
}
