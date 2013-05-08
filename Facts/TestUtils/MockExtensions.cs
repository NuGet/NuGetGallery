using System;
using System.Data;
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

        // Helper for mocking IEntitiesContext.Sql
        public static void SetupSql<TResult>(this Mock<IEntitiesContext> self, string query, Mock<IDataReader> mockReader, int? connectionTimeout = null, CommandBehavior behavior = CommandBehavior.Default)
        {
            self.Setup(
                e => e.Sql(
                    query,
                    It.IsAny<Func<IDataReader, TResult>>(),
                    connectionTimeout,
                    behavior))
                .Returns<string, Func<IDataReader, TResult>, int?, CommandBehavior>((q, cb, t, b) => cb(mockReader.Object));
        }
    }
}
