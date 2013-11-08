using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Queue.Protocol;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;

namespace NuGetGallery
{
    public static class StorageHelpers
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
            return CreateIfNotExistsAndRun(
                queue,
                act,
                QueueErrorCodeStrings.QueueNotFound,
                q => q.CreateIfNotExistsAsync());
        }

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
            return CreateIfNotExistsAndRun(
                container,
                act,
                BlobErrorCodeStrings.ContainerNotFound,
                c => c.CreateIfNotExistsAsync());
        }

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
            return CreateIfNotExistsAndRun(
                table,
                act,
                TableErrorCodeStrings.TableNotFound,
                t => t.CreateIfNotExistsAsync());
        }

        private static async Task<TResult> CreateIfNotExistsAndRun<TTarget, TResult>(TTarget target, Func<TTarget, Task<TResult>> act, string notFoundErrorCode, Func<TTarget, Task> createAction)
        {
            TResult result = default(TResult);
            bool retry = false;
            try
            {
                result = await act(target);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.ExtendedErrorInformation.ErrorCode == notFoundErrorCode)
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
