﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGetGallery.Areas.Admin;
using Xunit;

namespace NuGetGallery.Services
{
    public class AsynchronousDeleteAccountServiceFacts
    {
        private const string AccountDeleteMessageSchemaName = "AccountDeleteMessageData";

        public class TheDeleteGalleryUserAccountAsyncMethod
        {
            [Fact]
            public async Task DeleteUserEnqueues()
            {
                var username = "test";
                // Arrange
                var testUser = new User()
                {
                    Username = username
                };

                var testService = new TestAsynchronousDeleteAccountService(shouldFail: false);
                testService.SetupSimple();
                var deleteAccountService = testService.GetTestService();
                var messageSerializer = new AccountDeleteMessageSerializer();

                // Act
                var result = await deleteAccountService.DeleteAccountAsync(testUser, testUser, AccountDeletionOrphanPackagePolicy.UnlistOrphans);

                // Assert
                Assert.Equal(1, testService.TopicClient.SendAsyncCallCount);
                Assert.True(result.Success);
                Assert.Equal(string.Format(ServicesStrings.AsyncAccountDelete_Success, username), result.Description);

                var message = testService.TopicClient.LastSentMessage;
                Assert.NotNull(message);
                var messageData = messageSerializer.Deserialize(message);
                Assert.Equal(username, messageData.Username);
                Assert.Equal("GalleryUser", messageData.Source);
            }

            [Fact]
            public async Task DeleteUserAdminEnqueuesAdminSource()
            {
                var username = "test";
                // Arrange
                var testUser = new User()
                {
                    Username = username
                };

                var testAdmin = new User()
                {
                    Roles = new List<Role>() { new Role() { Name = "Admins" } }
                };

                var testService = new TestAsynchronousDeleteAccountService(shouldFail: false);
                testService.SetupSimple();
                var deleteAccountService = testService.GetTestService();
                var messageSerializer = new AccountDeleteMessageSerializer();

                // Act
                var result = await deleteAccountService.DeleteAccountAsync(testUser, testAdmin, AccountDeletionOrphanPackagePolicy.UnlistOrphans);

                // Assert
                Assert.Equal(1, testService.TopicClient.SendAsyncCallCount);
                Assert.True(result.Success);
                Assert.Equal(string.Format(ServicesStrings.AsyncAccountDelete_Success, username), result.Description);

                var message = testService.TopicClient.LastSentMessage;
                Assert.NotNull(message);
                var messageData = messageSerializer.Deserialize(message);
                Assert.Equal(username, messageData.Username);
                Assert.Equal("GalleryAdmin", messageData.Source);
            }

            [Fact]
            public async Task NullUserThrows()
            {
                // Arrange
                var testService = new TestAsynchronousDeleteAccountService(shouldFail: false);
                testService.SetupSimple();
                var deleteAccountService = testService.GetTestService();

                // Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() => deleteAccountService.DeleteAccountAsync(
                    null,
                    new User("AdminUser"),
                    orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.UnlistOrphans));
            }

            [Fact]
            public async Task FailedEnqueueStops()
            {
                // Arrange
                var testUser = new User()
                {
                    Username = "test"
                };

                var testService = new TestAsynchronousDeleteAccountService(shouldFail: true);
                testService.SetupSimple();
                var deleteAccountService = testService.GetTestService();

                var result = await deleteAccountService.DeleteAccountAsync(testUser, testUser, AccountDeletionOrphanPackagePolicy.UnlistOrphans);

                Assert.False(result.Success);
                Assert.Equal(ServicesStrings.AsyncAccountDelete_Fail, result.Description);
            }

            [Fact]
            public async Task FailedSupportRequestStops()
            {
                // Arrange
                var testUser = new User()
                {
                    Username = "test"
                };

                var testService = new TestAsynchronousDeleteAccountService(shouldFail: true);
                testService.SupportRequestMock
                    .Setup(sr => sr.TryAddDeleteSupportRequestAsync(It.IsAny<User>()))
                    .Returns(Task.FromResult(false));
                var deleteAccountService = testService.GetTestService();

                var result = await deleteAccountService.DeleteAccountAsync(testUser, testUser, AccountDeletionOrphanPackagePolicy.UnlistOrphans);

                Assert.False(result.Success);
                Assert.Equal(ServicesStrings.AccountDelete_CreateSupportRequestFails, result.Description);
            }
        }
 

        public class TestAsynchronousDeleteAccountService
        {
            public TestTopicClient TopicClient { get; set; }

            public Mock<ILogger<AsynchronousDeleteAccountService>> LoggerMock { get; set; }

            public Mock<ISupportRequestService> SupportRequestMock { get; set; }

            public TestAsynchronousDeleteAccountService(bool shouldFail)
            {
                TopicClient = new TestTopicClient(shouldFail);
                LoggerMock = new Mock<ILogger<AsynchronousDeleteAccountService>>();
                SupportRequestMock = new Mock<ISupportRequestService>();
            }

            public void SetupSimple()
            {
                SupportRequestMock.Setup(sr => sr.TryAddDeleteSupportRequestAsync(It.IsAny<User>()))
                    .Returns(Task.FromResult(true));
                //LoggerMock.Setup(l => l.LogError(It.IsAny<EventId>(), It.IsAny<Exception>(), It.IsAny<string>()));
            }

            public AsynchronousDeleteAccountService GetTestService()
            {
                return new AsynchronousDeleteAccountService(TopicClient, SupportRequestMock.Object, new AccountDeleteMessageSerializer(), LoggerMock.Object);
            }
        }

        public class TestTopicClient : ITopicClient
        {
            public bool ShouldFail { get; set; }

            public IBrokeredMessage LastSentMessage { get; private set; }

            public int SendAsyncCallCount { get; private set; }

            public TestTopicClient(bool shouldFail = false)
            {
                ShouldFail = shouldFail;
                SendAsyncCallCount = 0;
            }

            public Task SendAsync(IBrokeredMessage message)
            {
                Send(message);
                return Task.CompletedTask;
            }

            public void Send(IBrokeredMessage message)
            {
                ++SendAsyncCallCount;
                if (ShouldFail)
                {
                    throw new Exception();
                }

                LastSentMessage = message;
            }

            public void Close() => throw new NotImplementedException();
            public Task CloseAsync() => throw new NotImplementedException();
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