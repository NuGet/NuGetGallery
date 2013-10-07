using Moq.Language.Flow;
﻿using System.Threading.Tasks;
﻿using System.Collections.Generic;
using System.Linq;
using Moq;
using Moq.Language.Flow;

namespace NuGetGallery
{
    public static class MockExtensions
    {
        // Helper to get around Mock Returns((Type)null) weirdness.
        public static IReturnsResult<TMock> ReturnsNull<TMock, TRet>(this ISetup<TMock, TRet> self) where TMock: class where TRet: class
        {
            return self.Returns((TRet)null);
        }

        public static IReturnsResult<TMock> ReturnsNull<TMock, TRet>(this ISetup<TMock, Task<TRet>> self)
            where TMock : class
            where TRet : class
        {
            return self.Returns(Task.FromResult((TRet)null));
        }

        public static IReturnsResult<TMock> ReturnsAsync<TMock>(this ISetup<TMock, Task> self)
            where TMock : class
        {
            return self.Returns(Task.FromResult((object)null));
        }

        public static IReturnsResult<TMock> ReturnsAsync<TMock, TRet>(this ISetup<TMock, Task<TRet>> self, TRet value)
            where TMock : class
            where TRet : class
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

        public static void VerifyCommitted<T>(this Mock<IEntityRepository<T>> self)
            where T : class, IEntity, new()
        {
            self.Verify(e => e.CommitChanges());
        }

        public static void VerifyCommitted(this Mock<IEntitiesContext> self)
        {
            self.Verify(e => e.SaveChanges());
        }
    }
}
