 // Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class PackageOwnershipController : AdminControllerBase
    {
        private readonly IPackageService _packageService;
        private readonly IUserService _userService;
        private readonly IPackageOwnershipManagementService _packageOwnershipManagementService;

        public PackageOwnershipController(
            IPackageService packageService,
            IUserService userService,
            IPackageOwnershipManagementService packageOwnershipManagementService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _packageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
        }

        [HttpGet]
        public ViewResult Index(PackageOwnershipChangesInput input)
        {
            return View(input ?? new PackageOwnershipChangesInput());
        }

        /// <summary>
        /// This method does not perform any write operations. It just validates the input and models the changes that
        /// are going to be requested for confirmation.
        /// </summary>
        [HttpGet]
        public ViewResult ValidateInput(PackageOwnershipChangesInput input)
        {
            if (!ValidateInput(input, out var errorViewResult, out var validatedModel))
            {
                return errorViewResult;
            }

            return View(nameof(ValidateInput), validatedModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitInput(PackageOwnershipChangesInput input)
        {
            if (!ValidateInput(input, out var errorViewResult, out var validatedModel))
            {
                return errorViewResult;
            }

            foreach (var model in validatedModel.Changes)
            {
            }

            return Json(new { success = true });
        }

        private bool ValidateInput(PackageOwnershipChangesInput input, out ViewResult errorViewResult, out PackageOwnershipChangesModel validatedModel)
        {
            errorViewResult = null;
            validatedModel = null;

            // Find all of the users, by username
            var requestorUsername = (input.Requestor ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(requestorUsername))
            {
                errorViewResult = ShowError(input, nameof(PackageOwnershipChangesInput.Requestor), "You must provide a requestor username.");
                return false;
            }

            var addUsernames = SplitAndTrim(input.AddOwners, separator: ',');
            var removeUsernames = SplitAndTrim(input.RemoveOwners, separator: ',');
            if (addUsernames.Count == 0 && removeUsernames.Count == 0)
            {
                errorViewResult = ShowError(input, string.Empty, "You must provide either usernames to add or usernames to remove.");
                return false;
            }

            var usernameIntersection = addUsernames
                .Intersect(removeUsernames, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (usernameIntersection.Any())
            {
                errorViewResult = ShowError(input, string.Empty, "The set of usernames to add and remove must not intersect: " + string.Join(", ", usernameIntersection));
                return false;
            }

            var usernameToFields = new[] { (nameof(PackageOwnershipChangesInput.Requestor), requestorUsername) }
                .Concat(addUsernames.Select(x => (nameof(PackageOwnershipChangesInput.AddOwners), x)))
                .Concat(removeUsernames.Select(x => (nameof(PackageOwnershipChangesInput.RemoveOwners), x)))
                .GroupBy(x => x.Item2, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First().Item1, StringComparer.OrdinalIgnoreCase);
            var usernameToUser = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in usernameToFields)
            {
                var user = _userService.FindByUsername(pair.Key);
                if (user == null)
                {
                    errorViewResult = ShowError(input, pair.Value, $"The username '{pair.Key}' does not exist.");
                    return false;
                }

                usernameToUser.Add(user.Username, user);
            }

            if (usernameToUser[requestorUsername] is Organization)
            {
                errorViewResult = ShowError(input, nameof(PackageOwnershipChangesInput.Requestor), "The requestor cannot be an organization.");
                return false;
            }

            // Find all package registrations, by package ID
            var idToPackageRegistration = new SortedDictionary<string, PackageRegistration>(StringComparer.OrdinalIgnoreCase);
            foreach (var packageId in SplitAndTrim(input.PackageIds, separator: '\n'))
            {
                var packageRegistration = _packageService.FindPackageRegistrationById(packageId);
                if (packageRegistration == null)
                {
                    errorViewResult = ShowError(input, nameof(PackageOwnershipChangesInput.PackageIds), $"The package ID '{packageId}' does not exist.");
                    return false;
                }
                idToPackageRegistration.Add(packageRegistration.Id, packageRegistration);
            }

            if (idToPackageRegistration.Count == 0)
            {
                errorViewResult = ShowError(input, nameof(PackageOwnershipChangesInput.PackageIds), "You must provide at least one valid package ID.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(input.Message) && input.SkipRequestFlow)
            {
                errorViewResult = ShowError(input, nameof(PackageOwnershipChangesInput.Message), "The message is not allowed when skipping the request flow.");
                return false;
            }    

            // Determine the ownership status for each each relevant user, on each package
            validatedModel = CalculateChanges(
                idToPackageRegistration,
                usernameToUser,
                requestorUsername,
                addUsernames,
                removeUsernames,
                input.Message,
                input.SkipRequestFlow);
            return true;
        }

        private PackageOwnershipChangesModel CalculateChanges(
            IReadOnlyDictionary<string, PackageRegistration> idToPackageRegistration,
            Dictionary<string, User> usernameToUser,
            string requestorUsername,
            IReadOnlyList<string> addOwners,
            IReadOnlyList<string> removeOwners,
            string message,
            bool skipRequestFlow)
        {
            var requestor = usernameToUser[requestorUsername];

            var changes = new List<PackageRegistrationOwnershipChangeModel>();
            foreach (var packageRegistration in idToPackageRegistration.Values.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
            {
                var usernameToState = new SortedDictionary<string, PackageRegistrationUserChangeModel>(StringComparer.OrdinalIgnoreCase);

                InitializeExistingOwnersAndRequests(usernameToState, usernameToUser, packageRegistration);

                AddOwnersAndRequests(usernameToState, usernameToUser, requestor, addOwners, skipRequestFlow);

                RemoveOwnersAndRequests(usernameToState, removeOwners);

                // Fix up username casing to match the database. This makes usernames in views look like the user expects.
                foreach (var username in usernameToState.Keys.ToList())
                {
                    var state = usernameToState[username];
                    usernameToState.Remove(username);
                    usernameToState.Add(usernameToUser[username].Username, state);
                }

                var requestorHasPermissions = ActionsRequiringPermissions
                    .ManagePackageOwnership
                    .CheckPermissionsOnBehalfOfAnyAccount(usernameToUser[requestorUsername], packageRegistration) == PermissionsCheckResult.Allowed;

                changes.Add(new PackageRegistrationOwnershipChangeModel(
                    packageRegistration,
                    requestorHasPermissions,
                    usernameToState));
            }

            var normalizedInput = new PackageOwnershipChangesInput
            {
                Requestor = requestor.Username,
                PackageIds = string.Join(Environment.NewLine, idToPackageRegistration.Select(x => x.Value.Id)),
                AddOwners = string.Join(", ", addOwners.Select(x => usernameToUser[x].Username)),
                RemoveOwners = string.Join(", ", removeOwners.Select(x => usernameToUser[x].Username)),
                Message = message?.Trim() ?? string.Empty,
                SkipRequestFlow = skipRequestFlow,
            };

            return new PackageOwnershipChangesModel(
                normalizedInput,
                requestor,
                addOwners,
                removeOwners,
                changes);
        }

        private static void RemoveOwnersAndRequests(
            IDictionary<string, PackageRegistrationUserChangeModel> usernameToState,
            IReadOnlyList<string> removeOwners)
        {
            foreach (var removeUsername in removeOwners)
            {
                if (usernameToState.TryGetValue(removeUsername, out var model))
                {
                    switch (model.State)
                    {
                        case PackageOwnershipState.ExistingOwner:
                            usernameToState[removeUsername] = new PackageRegistrationUserChangeModel(
                                 PackageOwnershipState.RemoveOwner,
                                 model.Owner,
                                 model.Request);
                            break;
                        case PackageOwnershipState.ExistingOwnerRequest:
                            usernameToState[removeUsername] = new PackageRegistrationUserChangeModel(
                                 PackageOwnershipState.RemoveOwnerRequest,
                                 model.Owner,
                                 model.Request);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
                else
                {
                    usernameToState[removeUsername] = new PackageRegistrationUserChangeModel(
                        PackageOwnershipState.RemoveNoOp,
                        owner: null,
                        request: null);
                }
            }
        }

        private static void AddOwnersAndRequests(
            IDictionary<string, PackageRegistrationUserChangeModel> usernameToState,
            IReadOnlyDictionary<string, User> usernameToUser,
            User requestor,
            IReadOnlyList<string> addOwners,
            bool skipRequestFlow)
        {
            foreach (var addUsername in addOwners)
            {
                if (usernameToState.TryGetValue(addUsername, out var model))
                {
                    switch (model.State)
                    {
                        case PackageOwnershipState.ExistingOwner:
                            usernameToState[addUsername] = new PackageRegistrationUserChangeModel(
                                PackageOwnershipState.AlreadyOwner,
                                owner: model.Owner,
                                request: model.Request);
                            break;
                        case PackageOwnershipState.ExistingOwnerRequest:
                            usernameToState[addUsername] = new PackageRegistrationUserChangeModel(
                                PackageOwnershipState.AlreadyOwnerRequest,
                                owner: model.Owner,
                                request: model.Request);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
                else
                {
                    var newOwner = usernameToUser[addUsername];
                    var autoAccept = ActionsRequiringPermissions.HandlePackageOwnershipRequest.CheckPermissions(requestor, newOwner);

                    if (autoAccept == PermissionsCheckResult.Allowed || skipRequestFlow)
                    {
                        usernameToState[addUsername] = new PackageRegistrationUserChangeModel(
                            PackageOwnershipState.NewOwner,
                            owner: newOwner,
                            request: null);
                    }
                    else
                    {
                        usernameToState[addUsername] = new PackageRegistrationUserChangeModel(
                            PackageOwnershipState.NewOwnerRequest,
                            owner: newOwner,
                            request: null);
                    }
                }
            }
        }

        private void InitializeExistingOwnersAndRequests(
            IDictionary<string, PackageRegistrationUserChangeModel> usernameToState,
            IDictionary<string, User> usernameToUser,
            PackageRegistration packageRegistration)
        {
            foreach (var owner in packageRegistration.Owners)
            {
                usernameToState.Add(owner.Username, new PackageRegistrationUserChangeModel(
                    PackageOwnershipState.ExistingOwner,
                    owner: owner,
                    request: null));

                foreach (var user in packageRegistration.Owners)
                {
                    usernameToUser[user.Username] = user;
                }
            }

            var requests = _packageOwnershipManagementService.GetPackageOwnershipRequests(packageRegistration);
            foreach (var request in requests)
            {
                if (!usernameToState.ContainsKey(request.NewOwner.Username))
                {
                    usernameToState.Add(request.NewOwner.Username, new PackageRegistrationUserChangeModel(
                        PackageOwnershipState.ExistingOwnerRequest,
                        owner: null,
                        request: request));
                }

                usernameToUser[request.NewOwner.Username] = request.NewOwner;
            }
        }

        private static IReadOnlyList<string> SplitAndTrim(string input, char separator)
        {
            return (input ?? string.Empty)
                .Split(new[] { separator })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private ViewResult ShowError(PackageOwnershipChangesInput input, string key, string message)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            ModelState.AddModelError(key, message);
            return View(nameof(Index), input);
        }
    }
}