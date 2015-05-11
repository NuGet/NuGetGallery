// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Moq.Language.Flow;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGetGallery.Authentication;
using System;
using Xunit;
using Moq.Language;

namespace NuGetGallery
{
    public static class MockExtensions
    {
        // Helper to get around Mock Returns((Type)null) weirdness.
        public static IReturnsResult<TMock> ReturnsNull<TMock, TRet>(this IReturns<TMock, TRet> self)
            where TMock : class
            where TRet : class
        {
            return self.Returns((TRet)null);
        }

        public static IReturnsResult<TMock> CompletesWithNull<TMock, TRet>(this IReturns<TMock, Task<TRet>> self)
            where TMock : class
            where TRet : class
        {
            return self.Returns(Task.FromResult((TRet)null));
        }

        public static IReturnsResult<TMock> Completes<TMock>(this IReturns<TMock, Task> self)
            where TMock : class
        {
            return self.Returns(Task.FromResult((object)null));
        }

        public static IReturnsResult<TMock> CompletesWith<TMock, TRet>(this IReturns<TMock, Task<TRet>> self, TRet value)
            where TMock : class
        {
            return self.Returns(Task.FromResult(value));
        }

        public static IReturnsResult<IEntityRepository<T>> HasData<T>(this Mock<IEntityRepository<T>> self, params T[] fakeData)
            where T : class, IEntity, new()
        {
            return HasData(self, (IEnumerable<T>)fakeData);
        }

        public static IReturnsResult<IEntityRepository<T>> HasData<T>(this Mock<IEntityRepository<T>> self, IEnumerable<T> fakeData)
            where T : class, IEntity, new()
        {
            return self.Setup(e => e.GetAll()).Returns(fakeData.AsQueryable());
        }

        public static void VerifyCommitChanges(this IEntitiesContext self)
        {
            FakeEntitiesContext context = Assert.IsAssignableFrom<FakeEntitiesContext>(self);
            context.VerifyCommitChanges();
        }

        public static void VerifyCommitted<T>(this Mock<IEntityRepository<T>> self)
            where T : class, IEntity, new()
        {
            self.Verify(e => e.CommitChanges());
        }

        public static void VerifyCommitted(this Mock<IEntitiesContext> self)
        {
            self.Verify(e => e.SaveChanges());
        }

        public static IReturnsResult<AuthenticationService> SetupAuth(this Mock<AuthenticationService> self, Credential cred, User user)
        {
            return self.Setup(us => us.Authenticate(It.Is<Credential>(c =>
                String.Equals(c.Type, cred.Type, StringComparison.OrdinalIgnoreCase) &&
                String.Equals(c.Value, cred.Value, StringComparison.Ordinal))))
                .Returns(user == null ? null : new AuthenticatedUser(user, cred));
        }
    }
}

