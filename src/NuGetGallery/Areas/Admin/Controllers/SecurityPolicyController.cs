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
using NuGetGallery.Services.Security;

namespace NuGetGallery.Areas.Admin.Controllers
{
    /// <summary>
    /// Controller for the security policy management Admin view.
    /// </summary>
    public class SecurityPolicyController : AdminControllerBase
    {
        protected IEntitiesContext EntitiesContext { get; set; }

        protected ISecurityPolicyService PolicyService { get; set; }

        protected SecurityPolicyController()
        {
        }

        public SecurityPolicyController(IEntitiesContext entitiesContext, ISecurityPolicyService policyService)
        {
            EntitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            PolicyService = policyService ?? throw new ArgumentNullException(nameof(policyService));
        }

        [HttpGet]
        public virtual ActionResult Index()
        {
            var model = new SecurityPolicyViewModel()
            {
                SubscriptionNames = PolicyService.Subscriptions.Select(s => s.SubscriptionName)
            };

            return View(model);
        }

        [HttpGet]
        public virtual JsonResult Search(string query)
        {
            // Parse query and look for users in the DB.
            var usernames = GetUsernamesFromQuery(query ?? "");
            var users = EntitiesContext.Users
                .Where(u => usernames.Any(name => u.Username == name))
                .ToList();
            var usersNotFound = usernames.Except(users.Select(u => u.Username));

            var results = new UserSecurityPolicySearchResult()
            {
                // Found users and subscribed status for each policy subscription.
                Users = users.Select(u => new UserSecurityPolicySubscriptions()
                {
                    Username = u.Username,
                    Subscriptions = PolicyService.Subscriptions.ToDictionary(
                        s => s.SubscriptionName,
                        s => PolicyService.IsSubscribed(u, s))
                }),
                // Usernames that weren't found in the DB.
                UsersNotFound = usersNotFound
            };

            return Json(results, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> Update(List<string> subscriptionsJson)
        {
            var subscribeRequests =  subscriptionsJson?.Select(JsonConvert.DeserializeObject<JObject>)
                .Where(obj => obj["v"].ToObject<bool>())
                .GroupBy(obj => obj["u"].ToString())
                .ToDictionary(
                    g => g.Key, // username
                    g => g.Select(obj => obj["g"].ToString()) // subscriptions
                );
            
            var unsubscribeRequests = subscriptionsJson?.Select(JsonConvert.DeserializeObject<JObject>)
                .Where(obj => !obj["v"].ToObject<bool>())
                .GroupBy(obj => obj["u"].ToString())
                .ToDictionary(
                    g => g.Key, // username
                    g => g.Select(obj => obj["g"].ToString()) // subscriptions
                );

            foreach (var r in subscribeRequests)
            {
                var user = EntitiesContext.Users.FirstOrDefault(u => u.Username == r.Key);
                if (user != null)
                {
                    foreach (var subscription in r.Value)
                    {
                        await PolicyService.SubscribeAsync(user, subscription);
                    }
                }
            }

            foreach (var r in unsubscribeRequests)
            {
                var user = EntitiesContext.Users.FirstOrDefault(u => u.Username == r.Key);
                if (user != null)
                {
                    foreach (var subscription in r.Value)
                    {
                        await PolicyService.UnsubscribeAsync(user, subscription);
                    }
                }
            }

            return Json(new { success = true });
        }

        private static string[] GetUsernamesFromQuery(string query)
        {
            return query.Split(',', '\r', '\n')
                .Select(username => username.Trim())
                .Where(username => !string.IsNullOrEmpty(username)).ToArray();
        }
    }
}