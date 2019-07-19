// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
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
        private const int MAX_RETRY_COUNT = 5;

        private const string AccountDeleteMessageSchemaName = "AccountDeleteMessageData";
        private const string GalleryAccountDeleteMessageSourceName = "Gallery";

        private ITopicClient _topicClient;
        private IBrokeredMessageSerializer<AccountDeleteMessageData> _serializer;
        private ILogger<AsynchronousDeleteAccountService> _logger;

        public AsynchronousDeleteAccountService(ITopicClient topicClient, ILogger<AsynchronousDeleteAccountService> logger)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serializer = new BrokeredMessageSerializer<AccountDeleteMessageData>();
        }

        public async Task<DeleteAccountStatus> DeleteAccountAsync(User userToBeDeleted, User userToExecuteTheDelete, AccountDeletionOrphanPackagePolicy orphanPackagePolicy = AccountDeletionOrphanPackagePolicy.DoNotAllowOrphans)
        {
            var result = new DeleteAccountStatus()
            {
                AccountName = userToBeDeleted.Username
            };

            if (!UserIsAllowedToDelete(userToBeDeleted, userToExecuteTheDelete))
            {
                result.Success = false;
                result.Description = string.Format(CultureInfo.CurrentCulture,
                        ServicesStrings.AsyncAccountDelete_InsufficientPermissions,
                        userToExecuteTheDelete.Username,
                        userToBeDeleted.Username);

                return result;
            }

            var messageData = new AccountDeleteMessageData()
            {
                Username = userToBeDeleted.Username,
                Source = GalleryAccountDeleteMessageSourceName
            };

            var message = _serializer.Serialize(messageData);

            var enqueueSuccess = false;
            var canRetry = true;
            var retryCount = 0;

            while (!enqueueSuccess && retryCount < MAX_RETRY_COUNT && canRetry)
            {
                try
                {
                    await _topicClient.SendAsync(message);

                    // if SendAsync doesn't throw, as far as we can tell, the message went through.
                    enqueueSuccess = true;
                    result.Description = string.Format(CultureInfo.CurrentCulture,
                        ServicesStrings.AsyncAccountDelete_Success,
                        userToBeDeleted.Username);
                    result.Success = true;
                }
                catch (Exception ex)
                {
                    // This is the list of exceptions that are not retryable according to https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-exceptions
                    if (ex is InvalidOperationException
                        || ex is OperationCanceledException
                        || ex is ArgumentException
                        || ex is ArgumentNullException
                        || ex is ArgumentOutOfRangeException
                        || ex is MessagingEntityNotFoundException
                        || ex is MessageNotFoundException
                        || ex is MessageLockLostException
                        || ex is SessionLockLostException
                        || ex is MessagingEntityAlreadyExistsException
                        || ex is RuleActionException
                        || ex is FilterException
                        || ex is TransactionSizeExceededException
                        || ex is NoMatchingSubscriptionException
                        || ex is MessageSizeExceededException)
                    {

                        _logger.LogError(0, ex, "Failed to enqueue. Retrying was aborted due to exception");
                        result.Success = false;
                        result.Description = ServicesStrings.AsyncAccountDelete_NoRetryError;
                        canRetry = false;
                    }
                    else
                    {
                        retryCount++;
                        _logger.LogError(0, ex, "Failed to enqueue to AccountDeleter. Retrying with count: {RetryCount}", retryCount);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Determine if the user requesting the delete is allowed to request a delete of the account.
        /// This passes if the user requests their own delete, or if the user requesting is an admin.
        /// </summary>
        /// <param name="userToBeDeleted"></param>
        /// <param name="userRequestingDelete"></param>
        /// <returns></returns>
        private bool UserIsAllowedToDelete(User userToBeDeleted, User userRequestingDelete)
        {
            if (userRequestingDelete.MatchesUser(userToBeDeleted) || userRequestingDelete.IsAdministrator)
            {
                return true;
            }

            return false;
        }

        [Schema(Name = AccountDeleteMessageSchemaName, Version = 1)]
        private struct AccountDeleteMessageData
        {
            public string Username { get; set; }
            public string Source { get; set; }
        }
    }
}
