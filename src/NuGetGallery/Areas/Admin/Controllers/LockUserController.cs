// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Auditing;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class LockUserController : AdminControllerBase
    {
        private readonly IEntityRepository<User> _userRepository;
        private readonly IAuditingService _auditingService;

        public LockUserController(
            IEntityRepository<User> userRepository,
            IAuditingService auditingService)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
        }

        [HttpGet]
        public virtual ActionResult Index()
        {
            var model = new LockUserViewModel();

            return View("LockIndex", model);
        }

        [HttpGet]
        public virtual ActionResult Search(string query)
        {
            var lines = Helpers.ParseQueryToLines(query);
            var users = GetUsers(lines);

            return View("LockIndex", new LockUserViewModel
            {
                Query = query,
                LockStates = users
                    .Select(x => new LockState() { Identifier = x.Username, IsLocked = x.IsLocked })
                    .ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Update(LockUserViewModel viewModel)
        {
            var counter = 0;
            viewModel = viewModel ?? new LockUserViewModel();

            if (viewModel.LockStates != null)
            {
                var usernamesFromRequest = viewModel.LockStates.Select(x => x.Identifier).ToList();
                var usersFromDb = GetUsers(usernamesFromRequest);
                var userStatesFromRequestDictionary = viewModel
                    .LockStates
                    .ToDictionary(x => x.Identifier);

                foreach (var user in usersFromDb)
                {
                    if (userStatesFromRequestDictionary.TryGetValue(user.Username, out var userStateRequest))
                    {
                        if (user.IsLocked != userStateRequest.IsLocked)
                        {
                            user.UserStatusKey = userStateRequest.IsLocked ? UserStatus.Locked : UserStatus.Unlocked;
                            counter++;
                            await _auditingService.SaveAuditRecordAsync(new UserAuditRecord(
                                user,
                                userStateRequest.IsLocked ? AuditedUserAction.Lock : AuditedUserAction.Unlock));
                        }
                    }
                }

                if (counter > 0)
                {
                    await _userRepository.CommitChangesAsync();
                }
            }

            TempData["Message"] = string.Format(CultureInfo.InvariantCulture, $"Lock state was updated for {counter} users.");

            return View("LockIndex", viewModel);
        }

        private IList<User> GetUsers(IReadOnlyList<string> usernames)
        {
            return _userRepository.GetAll().Where(x => usernames.Contains(x.Username)).ToList();
        }
    }
}