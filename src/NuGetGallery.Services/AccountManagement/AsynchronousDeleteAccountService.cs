// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
            if (userToBeDeleted == null)
            {
                throw new ArgumentNullException(nameof(userToBeDeleted));
            }

            var result = new DeleteAccountStatus()
            {
                AccountName = userToBeDeleted.Username
            };

            var messageData = new AccountDeleteMessageData()
            {
                Username = userToBeDeleted.Username,
                Source = GalleryAccountDeleteMessageSourceName
            };

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

        [Schema(Name = AccountDeleteMessageSchemaName, Version = 1)]
        private struct AccountDeleteMessageData
        {
            public string Username { get; set; }

            // This defines the origin of the outgoing message.
            // This source must be defined in the AccountDeleter configuration, or it will refuse to process the message.
            public string Source { get; set; }
        }
    }
}
