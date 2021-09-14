// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure.Mail.Messages;

namespace NuGetGallery
{
    public class PackageOwnershipManagementService : IPackageOwnershipManagementService
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageService _packageService;
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly IPackageOwnerRequestService _packageOwnerRequestService;
        private readonly IAuditingService _auditingService;
        private readonly IUrlHelper _urlHelper;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IMessageService _messageService;

        public PackageOwnershipManagementService(
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IReservedNamespaceService reservedNamespaceService,
            IPackageOwnerRequestService packageOwnerRequestService,
            IAuditingService auditingService,
            IUrlHelper urlHelper,
            IAppConfiguration appConfiguration,
            IMessageService messageService)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _packageOwnerRequestService = packageOwnerRequestService ?? throw new ArgumentNullException(nameof(packageOwnerRequestService));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _urlHelper = urlHelper ?? throw new ArgumentNullException(nameof(urlHelper));
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        }

        public async Task AddPackageOwnerWithMessagesAsync(PackageRegistration packageRegistration, User user)
        {
            await AddPackageOwnerAsync(packageRegistration, user, commitChanges: true);

            var packageUrl = _urlHelper.Package(packageRegistration.Id, version: null, relativeUrl: false);

            // Accumulate the tasks so that they are sent in parallel and as many messages as possible are sent even if
            // one fails (i.e. throws an exception).
            var sendTasks = new List<Task>();
            foreach (var owner in packageRegistration.Owners)
            {
                var emailMessage = new PackageOwnerAddedMessage(_appConfiguration, owner, user, packageRegistration, packageUrl);
                sendTasks.Add(_messageService.SendMessageAsync(emailMessage));
            }

            await Task.WhenAll(sendTasks);
        }

        public async Task AddPackageOwnerAsync(PackageRegistration packageRegistration, User user, bool commitChanges = true)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (commitChanges)
            {
                using (var strategy = new SuspendDbExecutionStrategy())
                using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
                {
                    await AddPackageOwnerTask(packageRegistration, user, commitChanges);

                    transaction.Commit();
                }
            }
            else
            {
                await AddPackageOwnerTask(packageRegistration, user, commitChanges);
            }

            await _auditingService.SaveAuditRecordAsync(
                new PackageRegistrationAuditRecord(packageRegistration, AuditedPackageRegistrationAction.AddOwner, user.Username));
        }

        private async Task AddPackageOwnerTask(PackageRegistration packageRegistration, User user, bool commitChanges = true)
        {
            Func<ReservedNamespace, bool> predicate =
                    reservedNamespace => reservedNamespace.IsPrefix
                        ? packageRegistration.Id.StartsWith(reservedNamespace.Value, StringComparison.OrdinalIgnoreCase)
                        : packageRegistration.Id.Equals(reservedNamespace.Value, StringComparison.OrdinalIgnoreCase);

            var userOwnedMatchingNamespacesForId = user
                .ReservedNamespaces
                .Where(predicate);

            if (userOwnedMatchingNamespacesForId.Any())
            {
                if (!packageRegistration.IsVerified)
                {
                    await _packageService.UpdatePackageVerifiedStatusAsync(new List<PackageRegistration> { packageRegistration }, isVerified: true, commitChanges: commitChanges);
                }

                userOwnedMatchingNamespacesForId
                    .ToList()
                    .ForEach(mn =>
                        _reservedNamespaceService.AddPackageRegistrationToNamespace(mn.Value, packageRegistration));

                if (commitChanges)
                {
                    // The 'AddPackageRegistrationToNamespace' does not commit its changes, so saving changes for consistency.
                    await _entitiesContext.SaveChangesAsync();
                }
            }

            await _packageService.AddPackageOwnerAsync(packageRegistration, user, commitChanges);

            // Delete any ownership request related to this new owner, since the work is done. Don't audit since the
            // "add owner" operation is audited itself. Having a "delete package ownership" audit here would be confusing
            // because the intended user action was to add an owner, not to reject (delete) an ownership request.
            await DeletePackageOwnershipRequestAsync(packageRegistration, user, commitChanges, saveAudit: false);
        }

        public async Task<PackageOwnerRequest> AddPackageOwnershipRequestWithMessagesAsync(
            PackageRegistration packageRegistration,
            User requestingOwner,
            User newOwner,
            string message)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            if (requestingOwner == null)
            {
                throw new ArgumentNullException(nameof(requestingOwner));
            }

            if (newOwner == null)
            {
                throw new ArgumentNullException(nameof(newOwner));
            }

            var encodedMessage = HttpUtility.HtmlEncode(message ?? string.Empty);

            var packageUrl = _urlHelper.Package(packageRegistration.Id, version: null, relativeUrl: false);

            var ownerRequest = await AddPackageOwnershipRequestAsync(
                packageRegistration, requestingOwner, newOwner);

            var confirmationUrl = _urlHelper.ConfirmPendingOwnershipRequest(
                packageRegistration.Id,
                newOwner.Username,
                ownerRequest.ConfirmationCode,
                relativeUrl: false);

            var rejectionUrl = _urlHelper.RejectPendingOwnershipRequest(
                packageRegistration.Id,
                newOwner.Username,
                ownerRequest.ConfirmationCode,
                relativeUrl: false);

            var manageUrl = _urlHelper.ManagePackageOwnership(
                packageRegistration.Id,
                relativeUrl: false);

            var packageOwnershipRequestMessage = new PackageOwnershipRequestMessage(
                _appConfiguration,
                requestingOwner,
                newOwner,
                packageRegistration,
                packageUrl,
                confirmationUrl,
                rejectionUrl,
                encodedMessage,
                string.Empty);

            // Accumulate the tasks so that they are sent in parallel and as many messages as possible are sent even if
            // one fails (i.e. throws an exception).
            var messageTasks = new List<Task>();
            messageTasks.Add(_messageService.SendMessageAsync(packageOwnershipRequestMessage));

            foreach (var owner in packageRegistration.Owners)
            {
                var emailMessage = new PackageOwnershipRequestInitiatedMessage(
                    _appConfiguration,
                    requestingOwner,
                    owner,
                    newOwner,
                    packageRegistration,
                    manageUrl);

                messageTasks.Add(_messageService.SendMessageAsync(emailMessage));
            }

            await Task.WhenAll(messageTasks);

            return ownerRequest;
        }

        public async Task<PackageOwnerRequest> AddPackageOwnershipRequestAsync(PackageRegistration packageRegistration, User requestingOwner, User newOwner)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            if (requestingOwner == null)
            {
                throw new ArgumentNullException(nameof(requestingOwner));
            }

            if (newOwner == null)
            {
                throw new ArgumentNullException(nameof(newOwner));
            }

            var request = await _packageOwnerRequestService.AddPackageOwnershipRequest(packageRegistration, requestingOwner, newOwner);

            await _auditingService.SaveAuditRecordAsync(PackageRegistrationAuditRecord.CreateForAddOwnershipRequest(
                packageRegistration,
                requestingOwner.Username,
                newOwner.Username));

            return request;
        }

        public PackageOwnerRequest GetPackageOwnershipRequest(PackageRegistration package, User pendingOwner, string token)
        {
            return _packageOwnerRequestService.GetPackageOwnershipRequest(package, pendingOwner, token);
        }

        public IEnumerable<PackageOwnerRequest> GetPackageOwnershipRequests(PackageRegistration package = null, User requestingOwner = null, User newOwner = null)
        {
            return _packageOwnerRequestService.GetPackageOwnershipRequests(package, requestingOwner, newOwner);
        }

        public async Task RemovePackageOwnerWithMessagesAsync(PackageRegistration packageRegistration, User requestingOwner, User ownerToBeRemoved)
        {
            await RemovePackageOwnerAsync(packageRegistration, requestingOwner, ownerToBeRemoved);

            var emailMessage = new PackageOwnerRemovedMessage(_appConfiguration, requestingOwner, ownerToBeRemoved, packageRegistration);
            await _messageService.SendMessageAsync(emailMessage);
        }

        public async Task RemovePackageOwnerAsync(PackageRegistration packageRegistration, User requestingOwner, User ownerToBeRemoved, bool commitChanges = true)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            if (requestingOwner == null)
            {
                throw new ArgumentNullException(nameof(requestingOwner));
            }

            if (ownerToBeRemoved == null)
            {
                throw new ArgumentNullException(nameof(ownerToBeRemoved));
            }

            if (OwnerHasPermissionsToRemove(requestingOwner, ownerToBeRemoved, packageRegistration))
            {
                if (commitChanges)
                {
                    using (var strategy = new SuspendDbExecutionStrategy())
                    using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
                    {
                        await RemovePackageOwnerImplAsync(packageRegistration, ownerToBeRemoved);
                        transaction.Commit();
                    }
                }
                else
                {
                    await RemovePackageOwnerImplAsync(packageRegistration, ownerToBeRemoved, commitChanges: false);
                }

                await _auditingService.SaveAuditRecordAsync(
                    new PackageRegistrationAuditRecord(packageRegistration, AuditedPackageRegistrationAction.RemoveOwner, ownerToBeRemoved.Username));
            }
            else
            {
                throw new InvalidOperationException(string.Format(ServicesStrings.RemoveOwner_NotAllowed, requestingOwner.Username, ownerToBeRemoved.Username));
            }
        }

        private async Task RemovePackageOwnerImplAsync(PackageRegistration packageRegistration, User ownerToBeRemoved, bool commitChanges = true)
        {
            // Remove the user from owners list of package registration
            await _packageService.RemovePackageOwnerAsync(packageRegistration, ownerToBeRemoved, commitChanges: false);

            // Remove this package registration from the namespaces owned by this user that are owned by no other package owners
            foreach (var reservedNamespace in packageRegistration.ReservedNamespaces.ToArray())
            {
                if (!packageRegistration.Owners
                    .Any(o => ActionsRequiringPermissions.AddPackageToReservedNamespace
                        .CheckPermissionsOnBehalfOfAnyAccount(o, reservedNamespace) == PermissionsCheckResult.Allowed))
                {
                    _reservedNamespaceService.RemovePackageRegistrationFromNamespace(reservedNamespace, packageRegistration);
                }
            }

            // Remove the IsVerified flag from package registration if all the matching namespaces are owned by this user alone (no other package owner owns a matching namespace for this PR)
            if (packageRegistration.IsVerified && !packageRegistration.ReservedNamespaces.Any())
            {
                await _packageService.UpdatePackageVerifiedStatusAsync(new List<PackageRegistration> { packageRegistration }, isVerified: false, commitChanges: false);
            }

            if (commitChanges)
            {
                await _entitiesContext.SaveChangesAsync();
            }
        }

        public async Task CancelPackageOwnershipRequestWithMessagesAsync(PackageRegistration packageRegistration, User requestingOwner, User newOwner)
        {
            if (requestingOwner == null)
            {
                throw new ArgumentNullException(nameof(requestingOwner));
            }

            await DeletePackageOwnershipRequestAsync(packageRegistration, newOwner);

            var emailMessage = new PackageOwnershipRequestCanceledMessage(_appConfiguration, requestingOwner, newOwner, packageRegistration);
            await _messageService.SendMessageAsync(emailMessage);
        }

        public async Task DeclinePackageOwnershipRequestWithMessagesAsync(PackageRegistration packageRegistration, User requestingOwner, User newOwner)
        {
            if (requestingOwner == null)
            {
                throw new ArgumentNullException(nameof(requestingOwner));
            }

            await DeletePackageOwnershipRequestAsync(packageRegistration, newOwner);

            var emailMessage = new PackageOwnershipRequestDeclinedMessage(_appConfiguration, requestingOwner, newOwner, packageRegistration);
            await _messageService.SendMessageAsync(emailMessage);
        }

        public async Task DeletePackageOwnershipRequestAsync(PackageRegistration packageRegistration, User newOwner, bool commitChanges = true)
        {
            await DeletePackageOwnershipRequestAsync(packageRegistration, newOwner, commitChanges, saveAudit: true);
        }

        private async Task DeletePackageOwnershipRequestAsync(PackageRegistration packageRegistration, User newOwner, bool commitChanges, bool saveAudit)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            if (newOwner == null)
            {
                throw new ArgumentNullException(nameof(newOwner));
            }

            var request = _packageOwnerRequestService
                .GetPackageOwnershipRequestsWithUsers(package: packageRegistration, newOwner: newOwner)
                .FirstOrDefault();
            if (request != null)
            {
                // We must capture this audit record prior to the actual deletion operation. Deletion of an entity in
                // Entity Framework clears the relationship properties cause us to lose the information needed to create
                // the audit record. The package ID and usernames become unavailable.
                var auditRecord = PackageRegistrationAuditRecord.CreateForDeleteOwnershipRequest(
                    request.PackageRegistration,
                    request.RequestingOwner.Username,
                    request.NewOwner.Username);

                await _packageOwnerRequestService.DeletePackageOwnershipRequest(request, commitChanges);

                if (saveAudit)
                {
                    await _auditingService.SaveAuditRecordAsync(auditRecord);
                }
            }
        }

        private static bool OwnerHasPermissionsToRemove(User requestingOwner, User ownerToBeRemoved, PackageRegistration packageRegistration)
        {
            var reservedNamespaces = packageRegistration.ReservedNamespaces.ToList();
            if (ActionsRequiringPermissions.AddPackageToReservedNamespace
                .CheckPermissionsOnBehalfOfAnyAccount(ownerToBeRemoved, reservedNamespaces) == PermissionsCheckResult.Allowed)
            {
                // If the owner to be removed owns a reserved namespace that applies to this package,
                // the requesting user must own a reserved namespace that applies to this package or be a site admin.
                return ActionsRequiringPermissions.RemovePackageFromReservedNamespace
                    .CheckPermissionsOnBehalfOfAnyAccount(requestingOwner, reservedNamespaces) == PermissionsCheckResult.Allowed;
            }

            // If the owner to be removed does not own any reserved namespaces that apply to this package, they can be removed by anyone.
            return true;
        }
    }
}