// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Security;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class SecurityPolicyController : AdminControllerBase
    {
        public IEntitiesContext EntitiesContext { get; }

        public SecurityPolicyController(IEntitiesContext entitiesContext)
        {
            EntitiesContext = entitiesContext;
        }

        [HttpGet]
        public virtual ActionResult Index()
        {
            var model = new SecurityPolicyViewModel()
            {
                PolicyGroups = UserSecurityPolicyGroup.Instances.Select(pg => pg.Name)
            };

            return View(model);
        }

        [HttpGet]
        public virtual ActionResult Search(string query)
        {
            // Parse query and look for users in the DB.
            var usernames = GetUsernamesFromQuery(query);
            var users = FindUsers(usernames);
            var usersNotFound = usernames
                .Where(name => !users.Any(u => u.Username.Equals(name, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var results = new SecurityPolicySearchResult()
            {
                // Found users and enrollment status for each policy group.
                Users = users.Select(u => new SecurityPolicyEnrollments()
                {
                    Username = u.Username,
                    Enrollments = UserSecurityPolicyGroup.Instances.ToDictionary(
                        pg => pg.Name,
                        pg => u.IsEnrolled(pg))
                }),
                // Usernames that weren't found in the DB.
                UsersNotFound = usersNotFound
            };

            return Json(results, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Enroll(SecurityPolicyViewModel viewModel)
        {
            // Parse 'username|policyGroup' into enrollment requests by user.
            var enrollments = viewModel.Enrollments?
                .Select(e => e.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                .GroupBy(e => /*username*/e[0])
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => /*policyGroup*/e[1])
                );

            // Iterate all users and policies to identity groups for both enrollment and unenrollment.
            var usernames = GetUsernamesFromQuery(viewModel.Query);
            var users = FindUsers(usernames);
            foreach (var user in users)
            {
                foreach (var policyGroup in UserSecurityPolicyGroup.Instances)
                {
                    if (enrollments != null && enrollments[user.Username].Contains(policyGroup.Name))
                    {
                        user.AddPolicies(policyGroup);
                    }
                    else
                    {
                        var removedPolicies = user.RemovePolicies(policyGroup);
                        foreach (var p in removedPolicies)
                        {
                            EntitiesContext.UserSecurityPolicies.Remove(p);
                        }
                    }
                }
            }

            await EntitiesContext.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        private static string[] GetUsernamesFromQuery(string query)
        {
            return query.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(username => username.Trim()).ToArray();
        }

        private IEnumerable<User> FindUsers(string[] usernames)
        {
            return EntitiesContext.Users
                .Where(u => usernames.Any(name => u.Username.Equals(name, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
    }
}