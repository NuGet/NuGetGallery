using System.Collections.Generic;
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

        public static IReturnsResult<IEntityRepository<TType>> HasData<TType>(this Mock<IEntityRepository<TType>> self, params TType[] fakeData)
            where TType : class, IEntity, new()
        {
            return HasData(self, (IEnumerable<TType>)fakeData);
        }

        public static IReturnsResult<IEntityRepository<TType>> HasData<TType>(this Mock<IEntityRepository<TType>> self, IEnumerable<TType> fakeData)
            where TType : class, IEntity, new()
        {
            return self.Setup(e => e.GetAll()).Returns(fakeData.AsQueryable());
        }
    }
}
