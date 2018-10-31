// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Moq.Language;
using Moq.Language.Flow;
using NuGetGallery.Authentication;
using Xunit;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public static class MockExtensions
    {
        public static Mock<DbSet<T>> MockDbSet<T>(this IEnumerable<T> data)
           where T : class, IEntity
        {
            var query = data.AsQueryable();

            var dbSet = new Mock<DbSet<T>>();
            dbSet.As<IQueryable<T>>().Setup(s => s.Provider).Returns(query.Provider);
            dbSet.As<IQueryable<T>>().Setup(s => s.Expression).Returns(query.Expression);
            dbSet.As<IQueryable<T>>().Setup(s => s.ElementType).Returns(query.ElementType);
            dbSet.As<IQueryable<T>>().Setup(s => s.GetEnumerator()).Returns(() => data.GetEnumerator());

            return dbSet;
        }

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
            self.VerifyCommitted(Times.AtLeastOnce());
        }

        public static void VerifyCommitted<T>(this Mock<IEntityRepository<T>> self, Times times)
            where T : class, IEntity, new()
        {
            self.Verify(e => e.CommitChangesAsync(), times);
        }

        public static void VerifyCommitted(this Mock<IEntitiesContext> self)
        {
            self.VerifyCommitted(Times.AtLeastOnce());
        }

        public static void VerifyCommitted(this Mock<IEntitiesContext> self, Times times)
        {
            self.Verify(e => e.SaveChangesAsync(), times);
        }

        public static IReturnsResult<AuthenticationService> SetupAuth(this Mock<AuthenticationService> self, Credential cred, User user, string credentialValue = null)
        {
            if (CredentialTypes.IsApiKey(cred.Type))
            {
                return self.Setup(us => us.Authenticate(It.Is<string>(c =>
                            string.Equals(c, credentialValue ?? cred.Value, StringComparison.Ordinal))))
                    .Returns(Task.FromResult(user == null ? null : new AuthenticatedUser(user, cred)));
            }
            else
            {
                return self.Setup(us => us.Authenticate(It.Is<Credential>(c =>
                        string.Equals(c.Type, cred.Type, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Value, credentialValue ?? cred.Value, StringComparison.Ordinal))))
                    .Returns(Task.FromResult(user == null ? null : new AuthenticatedUser(user, cred)));
            }
        }
    }
}

