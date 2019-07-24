// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
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
                var messageSerializer = new BrokeredMessageSerializer<AccountDeleteMessageData>();

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
                Assert.Equal("Gallery", messageData.Source);
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
        }
 

        public class TestAsynchronousDeleteAccountService
        {
            public TestTopicClient TopicClient { get; set; }

            public Mock<ILogger<AsynchronousDeleteAccountService>> LoggerMock { get; set; }

            public TestAsynchronousDeleteAccountService(bool shouldFail)
            {
                TopicClient = new TestTopicClient(shouldFail);
                LoggerMock = new Mock<ILogger<AsynchronousDeleteAccountService>>();
            }

            public void SetupSimple()
            {
                //LoggerMock.Setup(l => l.LogError(It.IsAny<EventId>(), It.IsAny<Exception>(), It.IsAny<string>()));
            }

            public AsynchronousDeleteAccountService GetTestService()
            {
                return new AsynchronousDeleteAccountService(TopicClient, LoggerMock.Object);
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
                ++SendAsyncCallCount;
                if (ShouldFail)
                {
                    throw new Exception();
                }

                LastSentMessage = message;
                return Task.FromResult(0);
            }
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