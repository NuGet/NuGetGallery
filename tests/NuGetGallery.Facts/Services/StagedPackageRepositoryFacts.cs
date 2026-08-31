// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class StagedPackageRepositoryFacts
    {
        [Fact]
        public async Task CommitsTransactionAfterActionCompletes()
        {
            var transaction = new Mock<IDbContextTransaction>(MockBehavior.Strict);
            transaction.Setup(x => x.Commit());
            transaction.Setup(x => x.Dispose());
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(x => x.BeginTransaction())
                .Returns(transaction.Object);
            var entitiesContext = new Mock<IEntitiesContext>();
            entitiesContext
                .Setup(x => x.GetDatabase())
                .Returns(database.Object);
            var target = new StagedPackageRepository(entitiesContext.Object);
            var actionCompleted = false;

            await target.ExecuteInTransactionAsync(() =>
            {
                actionCompleted = true;
                return Task.CompletedTask;
            });

            Assert.True(actionCompleted);
            transaction.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task DoesNotCommitTransactionWhenActionFails()
        {
            var transaction = new Mock<IDbContextTransaction>();
            var database = new Mock<IDatabase>();
            database
                .Setup(x => x.BeginTransaction())
                .Returns(transaction.Object);
            var entitiesContext = new Mock<IEntitiesContext>();
            entitiesContext
                .Setup(x => x.GetDatabase())
                .Returns(database.Object);
            var target = new StagedPackageRepository(entitiesContext.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => target.ExecuteInTransactionAsync(
                    () => throw new InvalidOperationException()));

            transaction.Verify(x => x.Commit(), Times.Never);
            transaction.Verify(x => x.Dispose(), Times.Once);
        }
    }
}
