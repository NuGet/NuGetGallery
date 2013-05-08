using System;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Moq.Language.Flow;

namespace NuGetGallery
{
    public static class MockExtensions
    {
        // Helper for returning tasks
        public static IReturnsResult<TTarget> CompletesWith<TTarget, TInner>(this ISetup<TTarget, Task<TInner>> self, TInner inner) where TTarget : class
        {
            return self.Returns(Task.FromResult(inner));
        }

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

        // Helpers for mocking IFileStorageService
        public static void DoesNotContain(this Mock<IFileStorageService> self, string folderName, string fileName)
        {
            self.Setup(s => s.GetFileAsync(folderName, fileName)).CompletesWith((Stream)null);
        }

        public static void ContainsTextFile(this Mock<IFileStorageService> self, string folderName, string fileName, string text)
        {
            self.Setup(s => s.GetFileAsync(folderName, fileName))
                .CompletesWith(new MemoryStream(Encoding.UTF8.GetBytes(text)));
        }
    }
}
