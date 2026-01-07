// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Services.Authentication;

#nullable enable

namespace NuGetGallery.Areas.Admin.Controllers.FederatedCredentials
{
    public class ViewPoliciesViewModel
    {
        public ViewPoliciesViewModel(
            IReadOnlyList<string> usernames,
            IReadOnlyList<string> usernamesDoNoExist,
            IReadOnlyList<UserPoliciesViewModel> userPolices,
            AddPolicyViewModel addPolicy)
        {
            Usernames = usernames;
            UsernamesDoNotExist = usernamesDoNoExist;
            UserPolices = userPolices;
            AddPolicy = addPolicy;
        }

        public IReadOnlyList<string> Usernames { get; }
        public IReadOnlyList<string> UsernamesDoNotExist { get; }
        public IReadOnlyList<UserPoliciesViewModel> UserPolices { get; }
        public AddPolicyViewModel AddPolicy { get; }
    }

    public class AddPolicyViewModel
    {
        public string? PolicyName { get; set; }
        public string? PolicyUser { get; set; }
        public string? PolicyPackageOwner { get; set; }
        public FederatedCredentialType? PolicyType { get; set; }
        public string? PolicyCriteria { get; set; }
    }

    public class UserPoliciesViewModel
    {
        public UserPoliciesViewModel(User user, IReadOnlyList<FederatedCredentialPolicy> policies)
        {
            User = user;
            Policies = policies;
        }

        public User User { get; }
        public IReadOnlyList<FederatedCredentialPolicy> Policies { get; }
    }

    public class FederatedCredentialsController : AdminControllerBase
    {
        private readonly IEntityRepository<User> _userEntityRepository;
        private readonly IUserService _userService;
        private readonly IFederatedCredentialService _federatedCredentialService;

        public FederatedCredentialsController(
            IEntityRepository<User> userEntityRepository,
            IUserService userService,
            IFederatedCredentialService federatedCredentialService)
        {
            _userEntityRepository = userEntityRepository ?? throw new ArgumentNullException(nameof(userEntityRepository));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _federatedCredentialService = federatedCredentialService ?? throw new ArgumentNullException(nameof(federatedCredentialService));
        }

        [HttpGet]
        public ActionResult Index(
            string usernames = "",
            bool addOrganizationMembers = false,
            bool addUserOrganizations = false)
        {
            var splitUsernames = (usernames ?? string.Empty)
                .Split('\r', '\n')
                .Select(u => u.Trim())
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (splitUsernames.Count == 0
                && (addOrganizationMembers || addUserOrganizations))
            {
                // clear boolean query parameters, do nothing
                return RedirectToAction(nameof(Index));
            }

            var users = _userEntityRepository
                .GetAll()
                .Where(x => splitUsernames.Contains(x.Username))
                .Where(x => !x.IsDeleted)
                .Include(x => x.Credentials)
                .ToList();
            var foundUsernames = users
                .Select(x => x.Username)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (addOrganizationMembers)
            {
                splitUsernames.AddRange(users
                    .OfType<Organization>()
                    .SelectMany(x => x.Members)
                    .Select(x => x.Member.Username));
            }

            if (addUserOrganizations)
            {
                splitUsernames.AddRange(users
                    .SelectMany(x => x.Organizations)
                    .Select(x => x.Organization.Username));
            }

            if (addOrganizationMembers || addUserOrganizations)
            {
                splitUsernames = splitUsernames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                return RedirectToAction(nameof(Index), new { usernames = string.Join(Environment.NewLine, splitUsernames) });
            }

            var userKeys = users.Select(x => x.Key).ToList();
            var policies = _federatedCredentialService.GetPoliciesRelatedToUserKeys(userKeys);

            var userPoliciesViewModels = new List<UserPoliciesViewModel>();
            var remainingUsernames = new HashSet<string>(foundUsernames, StringComparer.OrdinalIgnoreCase);
            foreach (var group in policies.GroupBy(x => x.CreatedBy))
            {
                userPoliciesViewModels.Add(new UserPoliciesViewModel(
                    group.Key,
                    group.OrderBy(x => x.Created).ToList()));

                foreach (var policy in group)
                {
                    remainingUsernames.Remove(policy.CreatedBy.Username);
                    remainingUsernames.Remove(policy.PackageOwner.Username);
                }
            }

            foreach (var username in remainingUsernames)
            {
                var user = users.SingleOrDefault(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
                userPoliciesViewModels.Add(new UserPoliciesViewModel(user, []));
            }

            userPoliciesViewModels.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.User.Username, b.User.Username));

            return View(new ViewPoliciesViewModel(
                splitUsernames,
                splitUsernames.Except(foundUsernames, StringComparer.OrdinalIgnoreCase).ToList(),
                userPoliciesViewModels,
                new AddPolicyViewModel()));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeletePolicy(int policyKey)
        {
            var policy = _federatedCredentialService.GetPolicyByKey(policyKey);

            if (policy is null)
            {
                TempData["WarningMessage"] = $"The policy with key {policyKey} does not exist.";
                return RedirectToAction(nameof(Index));
            }

            var username = policy.CreatedBy.Username;

            await _federatedCredentialService.DeletePolicyAsync(policy);

            return RedirectForUser(username, $"Policy with key {policyKey} deleted successfully.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreatePolicy(AddPolicyViewModel addPolicy)
        {
            // Perform basic validation of incoming fields.
            bool isValid = true;
            if (string.IsNullOrWhiteSpace(addPolicy.PolicyUser))
            {
                AddModelError(nameof(FederatedCredentialPolicy.CreatedBy), "The policy user field is required.");
                isValid = false;
            }

            var createdBy = _userService.FindByUsername(addPolicy.PolicyUser);
            if (createdBy == null)
            {
                AddModelError(nameof(FederatedCredentialPolicy.CreatedBy), $"The policy user '{addPolicy.PolicyUser}' does not exist.");
                isValid = false;
            }

            if (!addPolicy.PolicyType.HasValue)
            {
                AddModelError(nameof(FederatedCredentialPolicy.Type), "The policy type field is required.");
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(addPolicy.PolicyCriteria))
            {
                AddModelError(nameof(FederatedCredentialPolicy.Criteria), "The policy criteria field is required.");
                isValid = false;
            }

            if (isValid)
            {
                var result = await _federatedCredentialService.AddPolicyAsync(
                            createdBy!,
                            addPolicy.PolicyPackageOwner!,
                            addPolicy.PolicyCriteria!,
                            addPolicy.PolicyName,
                            addPolicy.PolicyType!.Value);

                switch (result.Type)
                {
                    case FederatedCredentialPolicyValidationResultType.BadRequest:
                    case FederatedCredentialPolicyValidationResultType.Unauthorized:
                        AddModelError(result.PolicyPropertyName, result.UserMessage!);
                        break;
                    case FederatedCredentialPolicyValidationResultType.Success:
                        return RedirectForUser(result.Policy.CreatedBy.Username, $"Policy with key {result.Policy.Key} added successfully.");
                    default:
                        throw new NotImplementedException($"Unexpected result type: {result.Type}");
                }
            }

            return View(nameof(Index), new ViewPoliciesViewModel([], [], [], addPolicy));
        }

        private void AddModelError(string? policyPropertyName, string errorMessage)
        {
            const string modelPrefix = $"{nameof(ViewPoliciesViewModel.AddPolicy)}.";
            string key = policyPropertyName switch
            {
                nameof(FederatedCredentialPolicy.CreatedBy) =>
                    $"{modelPrefix}{nameof(AddPolicyViewModel.PolicyUser)}",
                nameof(FederatedCredentialPolicy.PackageOwner) =>
                    $"{modelPrefix}{nameof(AddPolicyViewModel.PolicyPackageOwner)}",
                nameof(FederatedCredentialPolicy.Type) =>
                    $"{modelPrefix}{nameof(AddPolicyViewModel.PolicyType)}",
                nameof(FederatedCredentialPolicy.Criteria) =>
                    $"{modelPrefix}{nameof(AddPolicyViewModel.PolicyCriteria)}",
                nameof(FederatedCredentialPolicy.PolicyName) =>
                    $"{modelPrefix}{nameof(AddPolicyViewModel.PolicyName)}",
                _ => nameof(ViewPoliciesViewModel.AddPolicy)
            };

            ModelState.AddModelError(key, errorMessage);
        }

        private RedirectResult RedirectForUser(string username, string message)
        {
            TempData["MessageFor" + username] = message;
            return Redirect(Url.Action(nameof(Index), new { usernames = username }) + $"#user-{username}");
        }
    }
}
