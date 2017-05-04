// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Security;

namespace NuGetGallery.Areas.Admin.Controllers
{
    /// <summary>
    /// Controller for the security policy management Admin view.
    /// </summary>
    public class SecurityPolicyController : AdminControllerBase
    {
        public IEntitiesContext EntitiesContext { get; }

        public ISecurityPolicyService PolicyService { get; }

        public SecurityPolicyController(IEntitiesContext entitiesContext, ISecurityPolicyService policyService)
        {
            if (entitiesContext == null)
            {
                throw new ArgumentNullException(nameof(entitiesContext));
            }
            if (policyService == null)
            {
                throw new ArgumentNullException(nameof(policyService));
            }

            EntitiesContext = entitiesContext;
            PolicyService = policyService;
        }

        [HttpGet]
        public virtual ActionResult Index()
        {
            var model = new SecurityPolicyViewModel()
            {
                SubscriptionNames = PolicyService.UserSubscriptions.Select(s => s.Name)
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

            var results = new UserSecurityPolicySearchResult()
            {
                // Found users and subscribed status for each policy subscription.
                Users = users.Select(u => new UserSecurityPolicySubscriptions()
                {
                    Username = u.Username,
                    Subscriptions = PolicyService.UserSubscriptions.ToDictionary(
                        s => s.Name,
                        s => PolicyService.IsSubscribed(u, s))
                }),
                // Usernames that weren't found in the DB.
                UsersNotFound = usersNotFound
            };

            return Json(results, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Update(SecurityPolicyViewModel viewModel)
        {
            // Policy subscription requests by user.
            var subscriptions = viewModel.UserSubscriptions?
                .Select(json => JsonConvert.DeserializeObject<JObject>(json))
                .GroupBy(obj => obj["u"].ToString())
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(obj => obj["g"].ToString())
                );

            // Iterate all users and groups to handle both subscribe and unsubscribe.
            var usernames = GetUsernamesFromQuery(viewModel.UsersQuery);
            var users = FindUsers(usernames);
            foreach (var user in users)
            {
                foreach (var subscription in PolicyService.UserSubscriptions)
                {
                    if (subscriptions != null && subscriptions[user.Username].Contains(subscription.Name))
                    {
                        await PolicyService.SubscribeAsync(user, subscription);
                    }
                    else
                    {
                        await PolicyService.UnsubscribeAsync(user, subscription);
                    }
                }
            }

            TempData["Message"] = $"Updated policies for {users.Count()} users.";

            return RedirectToAction("Index");
        }

        private static string[] GetUsernamesFromQuery(string query)
        {
            return query.Split(',', '\r', '\n')
                .Select(username => username.Trim())
                .Where(username => !string.IsNullOrEmpty(username)).ToArray();
        }

        private IEnumerable<User> FindUsers(string[] usernames)
        {
            return EntitiesContext.Users
                .Where(u => usernames.Any(name => u.Username.Equals(name, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
    }
}