using System.Threading.Tasks;
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
    }
}

