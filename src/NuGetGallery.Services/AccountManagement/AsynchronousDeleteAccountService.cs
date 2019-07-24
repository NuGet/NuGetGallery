// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        private const int MaxRetryCount = 5;

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

            while (!enqueueSuccess && retryCount < MaxRetryCount && canRetry)
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
                    if (UnretryableExceptionTypes.AnySafe(t => t.IsAssignableFrom(ex.GetType())))
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
            return userRequestingDelete.MatchesUser(userToBeDeleted) || userRequestingDelete.IsAdministrator;
        }

        // This is the list of exceptions that are not retryable according to https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-exceptions
        private IReadOnlyList<Type> UnretryableExceptionTypes = new Type[]
        {
            typeof(InvalidOperationException),
            typeof(OperationCanceledException),
            typeof(ArgumentException),
            typeof(ArgumentNullException),
            typeof(ArgumentOutOfRangeException),
            typeof(MessagingEntityNotFoundException),
            typeof(MessageNotFoundException),
            typeof(MessageLockLostException),
            typeof(SessionLockLostException),
            typeof(MessagingEntityAlreadyExistsException),
            typeof(RuleActionException),
            typeof(FilterException),
            typeof(TransactionSizeExceededException),
            typeof(NoMatchingSubscriptionException),
            typeof(MessageSizeExceededException)
        };

        [Schema(Name = AccountDeleteMessageSchemaName, Version = 1)]
        private struct AccountDeleteMessageData
        {
            public string Username { get; set; }
            public string Source { get; set; }
        }
    }
}
