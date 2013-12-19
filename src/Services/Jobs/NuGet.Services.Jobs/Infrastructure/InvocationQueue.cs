using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Queue.Protocol;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Configuration;
using NuGet.Services.Jobs.Infrastructure;
using NuGet.Services.Storage;

namespace NuGet.Services.Jobs
{
    public class InvocationQueue
    {
        public static readonly string DefaultQueueName = "nginvocations";

        private SqlConnectionStringBuilder _connectionString;

        protected InvocationQueue() { }

        public InvocationQueue(ConfigurationHub config)
            : this(config.Sql.GetConnectionString(KnownSqlServer.Primary)) { }

        public InvocationQueue(SqlConnectionStringBuilder connectionString)
            : this()
        {
            _connectionString = connectionString;
        }

        ///// <summary>
        ///// Dequeues the next request, if one is present
        ///// </summary>
        ///// <param name="invisibleFor">The period of time during which the message is invisble to other clients. The job must be <see cref="Acknowledge"/>d before this time or it will be dispatched again</param>
        //public virtual async Task<InvocationRequest> Dequeue(TimeSpan invisibleFor, CancellationToken token)
        //{
        //    // Get the ID of the next invocation to process from the queue
        //    var message = await _queue.Dequeue(invisibleFor, token);
            
        //    Guid invocationId;
        //    if (message == null)
        //    {
        //        return null;
        //    }
        //    // Parse the ID
        //    else if(!Guid.TryParse(message.AsString, out invocationId))
        //    {
        //        throw new FormatException(String.Format(
        //            CultureInfo.CurrentCulture,
        //            Strings.InvocationsQueue_InvalidInvocationId,
        //            message.AsString));
        //    }
        //    else 
        //    {
        //        // Retrieve the invocation details from the Invocations table.
        //        var invocation = await _table.Get(invocationId);
        //        await _table.Update(invocation);
        //        return new InvocationRequest(invocation, message);
        //    }
        //}

        ///// <summary>
        ///// Acknowledges that the request has completed successfully, removing the message from the queue.
        ///// </summary>
        ///// <param name="request">The request to acknowledge</param>
        //public virtual async Task Acknowledge(InvocationRequest request)
        //{
        //    if (request.Message != null)
        //    {
        //        await _queue.Delete(request.Message);
        //    }
        //}

        ///// <summary>
        ///// Extends the visibility timeout of the request. That is, the time during which the 
        ///// queue message is hidden from other clients
        ///// </summary>
        ///// <param name="request">The request to extend</param>
        ///// <param name="duration">The duration from the time of invocation to hide the message</param>
        //public virtual async Task Extend(InvocationRequest request, TimeSpan duration)
        //{
        //    if (request.Message != null)
        //    {
        //        await _queue.Update(request.Message, duration, MessageUpdateFields.Visibility);
        //    }
        //}

        //public virtual Task Update(Invocation invocation)
        //{
        //    invocation.Timestamp = DateTimeOffset.UtcNow;
        //    return _table.Update(invocation);
        //}

        //public virtual Task Enqueue(Invocation invocation)
        //{
        //    return EnqueueCore(invocation, visibilityTimeout: null);
        //}

        //public virtual Task Enqueue(Invocation invocation, TimeSpan visibilityTimeout)
        //{
        //    return EnqueueCore(invocation, visibilityTimeout);
        //}

        //private async Task EnqueueCore(Invocation invocation, TimeSpan? visibilityTimeout)
        //{
        //    // Create a queue message for this invocation
        //    var message = new CloudQueueMessage(invocation.Id.ToString());

        //    // Create an invocation entry and write it to the table
        //    invocation.Status = InvocationStatus.Queuing;
        //    await _table.Update(invocation);

        //    // Enqueue the message
        //    await _queue.Enqueue(message, visibilityTimeout, invocation.Id.ToString("N").ToLowerInvariant());

        //    // Update the invocation entry
        //    invocation.QueuedAt = DateTimeOffset.UtcNow;
        //    invocation.Status = InvocationStatus.Queued;
        //    invocation.EstimatedNextVisibleTime = DateTimeOffset.UtcNow + visibilityTimeout;
        //    await _table.Update(invocation);
        //}

        //internal Task Suspend(int p, JobContinuation jobContinuation)
        //{
        //    throw new NotImplementedException();
        //}

        //internal Task Complete(int p1, ExecutionResult executionResult, string p2)
        //{
        //    throw new NotImplementedException();
        //}

        //internal Task SetStatus(int p, InvocationStatus invocationStatus)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
