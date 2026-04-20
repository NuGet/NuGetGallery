// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;

namespace NuGetGallery
{
	public class LockUserService : ILockUserService
	{
		private readonly IEntityRepository<User> _userRepository;
		private readonly IAuditingService _auditingService;

		public LockUserService(
			IEntityRepository<User> userRepository,
			IAuditingService auditingService)
		{
			_userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
			_auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
		}

		public async Task<LockUserServiceResult> SetLockStateAsync(string username, bool isLocked)
		{
			if (string.IsNullOrWhiteSpace(username))
			{
				throw new ArgumentException("Username must not be null or empty.", nameof(username));
			}

			var user = _userRepository
				.GetAll()
				.SingleOrDefault(u => u.Username == username);

			if (user == null)
			{
				return LockUserServiceResult.UserNotFound;
			}

			if (user.IsLocked != isLocked)
			{
				user.UserStatusKey = isLocked ? UserStatus.Locked : UserStatus.Unlocked;

				await _auditingService.SaveAuditRecordAsync(new UserAuditRecord(
					user,
					isLocked ? AuditedUserAction.Lock : AuditedUserAction.Unlock));

				await _userRepository.CommitChangesAsync();
			}

			return LockUserServiceResult.Success;
		}
	}
}
