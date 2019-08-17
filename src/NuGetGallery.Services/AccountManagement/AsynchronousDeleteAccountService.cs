// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery
{
    /// <summary>
    /// Sends a message to a service bus queue when an account delete is requested.
    /// A request is considered to be successful if the message is enqueued to the service bus successfully.
    /// Retrys will be attempted until the limit is reached or an exception that cannot be retried is recieved
    /// </summary>
    public class AsynchronousDeleteAccountService : IDeleteAccountService
    {
        private const string GalleryUserAccountDeleteMessageSourceName = "GalleryUser";
        private const string GalleryAdminAccountDeleteMessageSourceName = "GalleryAdmin";

        private ITopicClient _topicClient;
        private ISupportRequestService _supportRequestService;
        private IBrokeredMessageSerializer<AccountDeleteMessage> _serializer;
        private ILogger<AsynchronousDeleteAccountService> _logger;

        public AsynchronousDeleteAccountService(
            ITopicClient topicClient,
            ISupportRequestService supportRequestService,
            IBrokeredMessageSerializer<AccountDeleteMessage> accountDeleteMessageSerializer,
            ILogger<AsynchronousDeleteAccountService> logger)

        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _supportRequestService = supportRequestService ?? throw new ArgumentNullException(nameof(supportRequestService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serializer = accountDeleteMessageSerializer ?? throw new ArgumentNullException(nameof(accountDeleteMessageSerializer));
        }

        public async Task<DeleteAccountStatus> DeleteAccountAsync(User userToBeDeleted, User userToExecuteTheDelete, AccountDeletionOrphanPackagePolicy orphanPackagePolicy = AccountDeletionOrphanPackagePolicy.DoNotAllowOrphans)
        {
            if (userToBeDeleted == null)
            {
                throw new ArgumentNullException(nameof(userToBeDeleted));
            }

            var result = new DeleteAccountStatus()
            {
                AccountName = userToBeDeleted.Username
            };

            var isSupportRequestCreated = await _supportRequestService.TryAddDeleteSupportRequestAsync(userToBeDeleted);
            if (!isSupportRequestCreated)
            {
                result.Success = false;
                result.Description = ServicesStrings.AccountDelete_CreateSupportRequestFails;
                return result;
            }

            var sourceName = GalleryUserAccountDeleteMessageSourceName;
            if (userToExecuteTheDelete.IsAdministrator)
            {
                sourceName = GalleryAdminAccountDeleteMessageSourceName;
            }

            var messageData = new AccountDeleteMessage(userToBeDeleted.Username, source: sourceName);

            var message = _serializer.Serialize(messageData);

            try
            {
                await _topicClient.SendAsync(message);

                // if SendAsync doesn't throw, as far as we can tell, the message went through.
                result.Description = string.Format(CultureInfo.CurrentCulture,
                    ServicesStrings.AsyncAccountDelete_Success,
                    userToBeDeleted.Username);
                result.Success = true;
            }
            catch (Exception ex)
            {
                // See https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-exceptions for a list of possible exceptions
                _logger.LogError(0, ex, "Failed to enqueue to AccountDeleter.");
                result.Success = false;
                result.Description = ServicesStrings.AsyncAccountDelete_Fail;
            }

            return result;
        }
    }
}
