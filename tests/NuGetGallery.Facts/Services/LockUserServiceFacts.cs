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
    public class LockUserServiceFacts
    {
        private readonly Mock<IEntityRepository<User>> _userRepository;
        private readonly Mock<IAuditingService> _auditingService;
        private readonly LockUserService _service;

        public LockUserServiceFacts()
        {
            _userRepository = new Mock<IEntityRepository<User>>();
            _auditingService = new Mock<IAuditingService>();
            _service = new LockUserService(_userRepository.Object, _auditingService.Object);
        }

        private static User CreateUser(string username, UserStatus status = UserStatus.Unlocked)
        {
            return new User(username)
            {
                UserStatusKey = status
            };
        }

        [Fact]
        public async Task ReturnsUserNotFoundWhenUserDoesNotExist()
        {
            _userRepository
                .Setup(r => r.GetAll())
                .Returns(new List<User>().AsQueryable());

            var result = await _service.SetLockStateAsync("nonexistent", true);

            Assert.Equal(LockUserServiceResult.UserNotFound, result);
        }

        [Fact]
        public async Task SetsUserStatusToLockedWhenLocking()
        {
            var user = CreateUser("testuser");
            _userRepository
                .Setup(r => r.GetAll())
                .Returns(new List<User> { user }.AsQueryable());

            var result = await _service.SetLockStateAsync("testuser", true);

            Assert.Equal(LockUserServiceResult.Success, result);
            Assert.Equal(UserStatus.Locked, user.UserStatusKey);
        }

        [Fact]
        public async Task SetsUserStatusToUnlockedWhenUnlocking()
        {
            var user = CreateUser("testuser", UserStatus.Locked);
            _userRepository
                .Setup(r => r.GetAll())
                .Returns(new List<User> { user }.AsQueryable());

            var result = await _service.SetLockStateAsync("testuser", false);

            Assert.Equal(LockUserServiceResult.Success, result);
            Assert.Equal(UserStatus.Unlocked, user.UserStatusKey);
        }

        [Fact]
        public async Task CommitsChangesWhenStateChanges()
        {
            var user = CreateUser("testuser");
            _userRepository
                .Setup(r => r.GetAll())
                .Returns(new List<User> { user }.AsQueryable());

            await _service.SetLockStateAsync("testuser", true);

            _userRepository.Verify(r => r.CommitChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task WritesAuditRecordWithReasonAndCallerIdentity()
        {
            var user = CreateUser("testuser");
            _userRepository
                .Setup(r => r.GetAll())
                .Returns(new List<User> { user }.AsQueryable());

            await _service.SetLockStateAsync("testuser", true, "TOS violation", "test-caller");

            _auditingService.Verify(
                a => a.SaveAuditRecordAsync(It.Is<UserAuditRecord>(r =>
                    r.Reason == "TOS violation" &&
                    r.CallerIdentity == "test-caller")),
                Times.Once);
        }

        [Fact]
        public async Task DoesNotAuditOrCommitWhenAlreadyLocked()
        {
            var user = CreateUser("testuser", UserStatus.Locked);
            _userRepository
                .Setup(r => r.GetAll())
                .Returns(new List<User> { user }.AsQueryable());

            var result = await _service.SetLockStateAsync("testuser", true);

            Assert.Equal(LockUserServiceResult.Success, result);
            _auditingService.Verify(
                a => a.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), Times.Never);
            _userRepository.Verify(
                r => r.CommitChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task DoesNotAuditOrCommitWhenAlreadyUnlocked()
        {
            var user = CreateUser("testuser");
            _userRepository
                .Setup(r => r.GetAll())
                .Returns(new List<User> { user }.AsQueryable());

            var result = await _service.SetLockStateAsync("testuser", false);

            Assert.Equal(LockUserServiceResult.Success, result);
            _auditingService.Verify(
                a => a.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), Times.Never);
            _userRepository.Verify(
                r => r.CommitChangesAsync(), Times.Never);
        }
    }
}
