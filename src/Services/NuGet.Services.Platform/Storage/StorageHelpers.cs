using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.WindowsAzure.Storage.Queue
{
    using NuGet.Services.Storage;

    public static class QueueHelpers
    {
        /// <summary>
        /// Executes the specified action, and if it throws an error because the queue does not exist, creates
        /// the queue and re-executes the action
        /// </summary>
        public static Task SafeExecute(this CloudQueue queue, Func<CloudQueue, Task> act)
        {
            return SafeExecute<object>(queue, async q =>
            {
                await act(q);
                return null;
            });
        }

        /// <summary>
        /// Executes the specified action, and if it throws an error because the queue does not exist, creates
        /// the queue and re-executes the action
        /// </summary>
        public static Task<T> SafeExecute<T>(this CloudQueue queue, Func<CloudQueue, Task<T>> act)
        {
            return StorageHelpers.SafeExecuteCore(
                queue,
                act,
                Protocol.QueueErrorCodeStrings.QueueNotFound,
                async q =>
                {
                    try
                    {
                        await q.CreateIfNotExistsAsync();
                    }
                    catch (StorageException ex)
                    {
                        if ((ex.InnerException is NullReferenceException) && ex.StackTrace.Contains("ProcessExpectedStatusCodeNoException"))
                        {
                            // Work around a bug in storage API
                            return;
                        }
                        throw;
                    }
                });
        }
    }
}

namespace Microsoft.WindowsAzure.Storage.Blob
{
    using NuGet.Services.Storage;

    public static class BlobHelpers
    {
        /// <summary>
        /// Executes the specified action, and if it throws an error because the container does not exist, creates
        /// the container and re-executes the action
        /// </summary>
        public static Task SafeExecute(this CloudBlobContainer container, Func<CloudBlobContainer, Task> act)
        {
            return SafeExecute<object>(container, async c =>
            {
                await act(c);
                return null;
            });
        }

        /// <summary>
        /// Executes the specified action, and if it throws an error because the container does not exist, creates
        /// the container and re-executes the action
        /// </summary>
        public static Task<T> SafeExecute<T>(this CloudBlobContainer container, Func<CloudBlobContainer, Task<T>> act)
        {
            return StorageHelpers.SafeExecuteCore(
                container,
                act,
                Protocol.BlobErrorCodeStrings.ContainerNotFound,
                c => c.CreateIfNotExistsAsync());
        }
    }
}

namespace Microsoft.WindowsAzure.Storage.Table
{
    using NuGet.Services.Storage;

    public static class TableHelpers
    {
        /// <summary>
        /// Executes the specified action, and if it throws an error because the table does not exist, creates
        /// the table and re-executes the action
        /// </summary>
        public static Task SafeExecute(this CloudTable table, Func<CloudTable, Task> act)
        {
            return SafeExecute<object>(table, async q =>
            {
                await act(q);
                return null;
            });
        }

        /// <summary>
        /// Executes the specified action, and if it throws an error because the table does not exist, creates
        /// the table and re-executes the action
        /// </summary>
        public static Task<T> SafeExecute<T>(this CloudTable table, Func<CloudTable, Task<T>> act)
        {
            return StorageHelpers.SafeExecuteCore(
                table,
                act,
                Protocol.TableErrorCodeStrings.TableNotFound,
                t => t.CreateIfNotExistsAsync());
        }
    }
}

namespace NuGet.Services.Storage
{
    public static class StorageHelpers
    {
        internal static async Task<TResult> SafeExecuteCore<TTarget, TResult>(TTarget target, Func<TTarget, Task<TResult>> act, string notFoundErrorCode, Func<TTarget, Task> createAction)
        {
            TResult result = default(TResult);
            bool retry = false;
            try
            {
                result = await act(target);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation != null &&
                    ex.RequestInformation.ExtendedErrorInformation != null &&
                    ex.RequestInformation.ExtendedErrorInformation.ErrorCode == notFoundErrorCode)
                {
                    retry = true; // Can't await in a catch block :(
                }
                else
                {
                    throw;
                }
            }

            if (retry)
            {
                await createAction(target);
                result = await act(target); // Let this exception throw
            }

            return result;
        }
    }
}
