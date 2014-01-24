using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Dapper;
using Microsoft.SqlServer.Server;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Queue.Protocol;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Configuration;
using NuGet.Services.ServiceModel;
using NuGet.Services.Storage;
using NuGet.Services.Work.Models;

namespace NuGet.Services.Work
{
    public class InvocationQueue
    {
        public static readonly InvocationQueue Null = new NullInvocationQueue();

        public static readonly string ArchiveContainer = "work-archive";

        private SqlConnectionStringBuilder _connectionString;
        private string _instanceName;
        private Clock _clock;
        private StorageHub _storage;

        protected InvocationQueue() { }

        public InvocationQueue(Clock clock, string instanceName, StorageHub storage, ConfigurationHub config)
            : this(clock, instanceName, storage, config.Sql.GetConnectionString(KnownSqlServer.Primary)) { }

        public InvocationQueue(Clock clock, string instanceName, StorageHub storage, SqlConnectionStringBuilder connectionString)
            : this()
        {
            _clock = clock;
            _storage = storage;
            _instanceName = instanceName;
            _connectionString = connectionString;
        }

        public virtual Task<InvocationState> Enqueue(string job, string source)
        {
            return Enqueue(job, source, null, TimeSpan.Zero, jobInstanceName: null, unlessAlreadyRunning: false);
        }

        public virtual Task<InvocationState> Enqueue(string job, string source, bool unlessAlreadyRunning)
        {
            return Enqueue(job, source, null, TimeSpan.Zero, jobInstanceName: null, unlessAlreadyRunning: unlessAlreadyRunning);
        }

        public virtual Task<InvocationState> Enqueue(string job, string source, Dictionary<string, string> payload)
        {
            return Enqueue(job, source, payload, TimeSpan.Zero, jobInstanceName: null, unlessAlreadyRunning: false);
        }

        public virtual Task<InvocationState> Enqueue(string job, string source, Dictionary<string, string> payload, bool unlessAlreadyRunning)
        {
            return Enqueue(job, source, payload, TimeSpan.Zero, jobInstanceName: null, unlessAlreadyRunning: unlessAlreadyRunning);
        }

        public virtual Task<InvocationState> Enqueue(string job, string source, Dictionary<string, string> payload, TimeSpan invisibleFor)
        {
            return Enqueue(job, source, payload, invisibleFor, jobInstanceName: null, unlessAlreadyRunning: false);
        }

        public virtual async Task<InvocationState> Enqueue(string job, string source, Dictionary<string, string> payload, TimeSpan invisibleFor, string jobInstanceName, bool unlessAlreadyRunning)
        {
            var invisibleUntil = _clock.UtcNow + invisibleFor;

            string payloadString = null;
            if (payload != null)
            {
                payloadString = InvocationPayloadSerializer.Serialize(payload);
            }

            var row = await ConnectAndExec(
                "work.EnqueueInvocation",
                new
                {
                    Job = job,
                    Source = source,
                    Payload = payloadString,
                    NextVisibleAt = invisibleUntil.UtcDateTime,
                    InstanceName = _instanceName,
                    JobInstanceName = jobInstanceName,
                    UnlessAlreadyRunning = unlessAlreadyRunning
                });
            if (row == null)
            {
                return null;
            }
            return new InvocationState(row);
        }

        /// <summary>
        /// Dequeues the next request, if one is present
        /// </summary>
        /// <param name="invisibleFor">The period of time during which the message is invisble to other clients. The job must be <see cref="Complete"/>d before this time or it will be dispatched again</param>
        public virtual async Task<InvocationState> Dequeue(TimeSpan invisibleFor, CancellationToken token)
        {
            var invisibleUntil = _clock.UtcNow + invisibleFor;
            var row = await ConnectAndExec(
                "work.DequeueInvocation",
                new
                {
                    InstanceName = _instanceName,
                    HideUntil = invisibleUntil.UtcDateTime
                });

            if (row == null)
            {
                return null;
            }
            else
            {
                return new InvocationState(row);
            }
        }

        /// <summary>
        /// Acknowledges that the request has completed successfully, removing the message from the queue.
        /// </summary>
        /// <param name="request">The request to acknowledge</param>
        /// <returns>A boolean indicating if the request was successful. Failure indicates that another node has updated the invocation since we last checked</returns>
        public virtual async Task<bool> Complete(InvocationState invocation, ExecutionResult result, string resultMessage, string logUrl)
        {
            // Try to complete the row
            var newVersion = await ConnectAndExec(
                "work.CompleteInvocation",
                new
                {
                    Id = invocation.Id,
                    Version = invocation.CurrentVersion,
                    Result = (int)result,
                    ResultMessage = resultMessage,
                    LogUrl = logUrl,
                    InstanceName = _instanceName
                });
            return ProcessResult(invocation, newVersion);
        }

        /// <summary>
        /// Extends the visibility timeout of the request. That is, the time during which the 
        /// queue message is hidden from other clients
        /// </summary>
        /// <param name="request">The request to extend</param>
        /// <param name="duration">The duration from the time of invocation to hide the message</param>
        public virtual async Task<bool> Extend(InvocationState invocation, TimeSpan duration)
        {
            var invisibleUntil = _clock.UtcNow + duration;
            var newVersion = await ConnectAndExec(
                "work.ExtendInvocation",
                new
                {
                    Id = invocation.Id,
                    Version = invocation.CurrentVersion,
                    ExtendTo = invisibleUntil.UtcDateTime,
                    InstanceName = _instanceName
                });

            return ProcessResult(invocation, newVersion);
        }

        public virtual async Task<bool> Suspend(InvocationState invocation, Dictionary<string, string> newPayload, TimeSpan suspendFor, string logUrl)
        {
            var suspendUntil = _clock.UtcNow + suspendFor;
            var serializedPayload = InvocationPayloadSerializer.Serialize(newPayload);
            var newVersion = await ConnectAndExec(
                "work.SuspendInvocation",
                new
                {
                    Id = invocation.Id,
                    Version = invocation.CurrentVersion,
                    Payload = serializedPayload,
                    SuspendUntil = suspendUntil.UtcDateTime,
                    LogUrl = logUrl,
                    InstanceName = _instanceName
                });
            return ProcessResult(invocation, newVersion);
        }

        public virtual async Task<bool> UpdateStatus(InvocationState invocation, InvocationStatus status, ExecutionResult result)
        {
            var newVersion = await ConnectAndExec(
                "work.SetInvocationStatus",
                new
                {
                    Id = invocation.Id,
                    Version = invocation.CurrentVersion,
                    Status = status,
                    Result = result,
                    InstanceName = _instanceName
                });
            return ProcessResult(invocation, newVersion);
        }

        public virtual Task<InvocationState> GetMostRecentState(string jobName)
        {
            return ConnectAndQuerySingle(@"
                SELECT TOP 1 *
                FROM work.Invocations WHERE [Job] = @jobName
                ORDER BY [UpdatedAt] DESC",
                new { jobName });
        }

        public virtual Task<IEnumerable<InvocationState>> GetAll(InvocationListCriteria criteria)
        {
            return GetAll(criteria, limit: null);
        }

        public virtual Task<IEnumerable<InvocationState>> GetAll(InvocationListCriteria criteria, int? limit)
        {
            string subquery;
            object parameters;
            switch (criteria)
            {
                case InvocationListCriteria.All:
                    subquery = "FROM work.Invocations";
                    parameters = null;
                    break;
                case InvocationListCriteria.Active:
                    subquery = "FROM work.ActiveInvocations";
                    parameters = null;
                    break;
                case InvocationListCriteria.Completed:
                    subquery = "FROM work.Invocations WHERE Complete = 1";
                    parameters = null;
                    break;
                case InvocationListCriteria.Pending:
                    subquery = "FROM work.Invocations WHERE [NextVisibleAt] <= @p";
                    parameters = new { now = _clock.UtcNow.UtcDateTime };
                    break;
                case InvocationListCriteria.Hidden:
                    subquery = "FROM work.ActiveInvocations WHERE [NextVisibleAt] > @now";
                    parameters = new { now = _clock.UtcNow.UtcDateTime };
                    break;
                case InvocationListCriteria.Suspended:
                    subquery = "FROM work.ActiveInvocations WHERE [Status] = @status";
                    parameters = new { status = InvocationStatus.Suspended };
                    break;
                case InvocationListCriteria.Executing:
                    subquery = "FROM work.ActiveInvocations WHERE [Status] = @status";
                    parameters = new { status = InvocationStatus.Executing };
                    break;
                default:
                    return Task.FromResult(Enumerable.Empty<InvocationState>());
            }
            return ConnectAndQuery(
                "SELECT" +
                (limit.HasValue ? (" TOP " + limit.Value.ToString()) : String.Empty) +
                " * " +
                subquery +
                " ORDER BY [UpdatedAt] DESC", parameters);
        }

        public virtual async Task<InvocationStatisticsRecord> GetStatistics()
        {
            return (await GetStatisticsCore("InvocationStatistics")).SingleOrDefault();
        }

        public virtual Task<IEnumerable<InvocationStatisticsRecord>> GetJobStatistics()
        {
            return GetStatisticsCore("JobStatistics");
        }

        public virtual Task<IEnumerable<InvocationStatisticsRecord>> GetInstanceStatistics()
        {
            return GetStatisticsCore("InstanceStatistics");
        }

        public virtual Task<InvocationState> Get(Guid id)
        {
            return ConnectAndQuerySingle(
                "SELECT * FROM work.Invocations WHERE Id = @id",
                new { id });
        }

        public virtual Task Purge(Guid id)
        {
            return Purge(new [] { id });
        }

        public virtual async Task Purge(IEnumerable<Guid> ids)
        {
            if (ids.Any())
            {
                using (var connection = await Connect())
                {
                    // First, capture the invocation history into blobs
                    var rows = await connection.QueryAsync<InvocationState.InvocationRow>(
                        "work.GetInvocationHistory",
                        new { Ids = new IdListParameter(ids) },
                        commandType: CommandType.StoredProcedure);

                    // Group by Id
                    var invocationHistories = rows.GroupBy(r => r.Id);

                    // Record invocation histories using a dataflow so we can constraint the parallelism
                    var block = new ActionBlock<IOrderedEnumerable<InvocationState.InvocationRow>>(
                        i => ArchiveInvocation(i),
                        new ExecutionDataflowBlockOptions()
                        {
                            MaxDegreeOfParallelism = 16
                        });
                    foreach (var invocationHistory in invocationHistories)
                    {
                        block.Post(invocationHistory.OrderBy(h => h.UpdatedAt));
                    }
                    block.Complete();
                    await block.Completion;

                    // Now purge the invocations
                    await connection.QueryAsync<int>(
                        "work.PurgeInvocations",
                        new { Ids = new IdListParameter(ids) },
                        commandType: CommandType.StoredProcedure);
                }
            }
        }

        public virtual Task<IEnumerable<InvocationState>> GetPurgable()
        {
            return GetPurgable(DateTimeOffset.UtcNow);
        }

        public virtual Task<IEnumerable<InvocationState>> GetPurgable(DateTimeOffset before)
        {
            return ConnectAndQuery(@"
                SELECT *
                FROM [work].Invocations
                WHERE ([Result] IS NOT NULL AND [Result] <> @Incomplete)
                AND ([CompletedAt] IS NOT NULL AND [CompletedAt] < @CompletedBefore)",
                new { 
                    CompletedBefore = before.UtcDateTime,
                    Incomplete = ExecutionResult.Incomplete
                });
        }

        public Task<IEnumerable<InvocationState>> PurgeCompleted()
        {
            return PurgeCompleted(DateTimeOffset.UtcNow);
        }

        public async Task<IEnumerable<InvocationState>> PurgeCompleted(DateTimeOffset before)
        {
            // Get purgable invocations
            var purgable = await GetPurgable(before);

            // Purge 'em
            await Purge(purgable.Select(i => i.Id));

            // Return 'em
            return purgable;
        }

        private Task ArchiveInvocation(IOrderedEnumerable<InvocationState.InvocationRow> invocationHistory)
        {
            var latest = invocationHistory.Last();
            string name = String.Format(
                    "{0}_{1}_{2}.json",
                    latest.Job,
                    latest.UpdatedAt.ToString("s"),
                    latest.Id.ToString("N").ToLowerInvariant());
            return _storage.Primary.Blobs.UploadJsonBlob(invocationHistory.ToArray(), ArchiveContainer, name);
        }

        private async Task<IEnumerable<InvocationStatisticsRecord>> GetStatisticsCore(string view)
        {
            using (var connection = await Connect())
            {
                // Teeny tiny SQL Injection :)
                return await connection.QueryAsync<InvocationStatisticsRecord>("SELECT * FROM work." + view);
            }
        }

        private bool ProcessResult(InvocationState invocation, InvocationState.InvocationRow result)
        {
            if (result == null)
            {
                return false;
            }
            else
            {
                invocation.Update(result);
                return true;
            }
        }

        private async Task<SqlConnection> Connect()
        {
            var conn = new SqlConnection(_connectionString.ConnectionString);
            await conn.OpenAsync();
            return conn;
        }

        private async Task<InvocationState.InvocationRow> ConnectAndExec(string proc, object parameters)
        {
            using (var connection = await Connect())
            {
                return (await connection.QueryAsync<InvocationState.InvocationRow>(
                    proc,
                    parameters,
                    commandType: CommandType.StoredProcedure))
                    .SingleOrDefault();
            }
        }
        
        private async Task<IEnumerable<InvocationState>> ConnectAndQuery(string sql, object parameters = null)
        {
            using (var connection = await Connect())
            {
                return (await connection.QueryAsync<InvocationState.InvocationRow>(
                    sql,
                    parameters,
                    commandType: CommandType.Text)).Select(r => new InvocationState(r));
            }
        }

        private async Task<InvocationState> ConnectAndQuerySingle(string sql, object parameters = null)
        {
            return (await ConnectAndQuery(sql, parameters)).SingleOrDefault();
        }

        private class IdListParameter : SqlMapper.ICustomQueryParameter
        {
            // Based on https://gist.github.com/BlackjacketMack/7242538

            private IEnumerable<Guid> _ids;

            public IdListParameter(IEnumerable<Guid> ids)
            {
                _ids = ids;
            }

            public void AddParameter(IDbCommand command, string name)
            {
                var sqlCommand = (SqlCommand)command;

                var number_list = new List<SqlDataRecord>();

                var tvp_definition = new[] { new SqlMetaData("Value", SqlDbType.UniqueIdentifier) };

                foreach (Guid n in _ids)
                {
                    var rec = new SqlDataRecord(tvp_definition);
                    rec.SetGuid(0, n);
                    number_list.Add(rec);
                }

                var p = sqlCommand.Parameters.Add(name, SqlDbType.Structured);
                p.Direction = ParameterDirection.Input;
                p.TypeName = "[work].IdList";
                p.Value = number_list;
            }
        }

        private class NullInvocationQueue : InvocationQueue
        {
            public override Task<bool> Complete(InvocationState invocation, ExecutionResult result, string resultMessage, string logUrl)
            {
                return Task.FromResult(true);
            }

            public override Task<InvocationState> Dequeue(TimeSpan invisibleFor, CancellationToken token)
            {
                return Task.FromResult<InvocationState>(null);
            }

            public override Task<InvocationState> Enqueue(string job, string source, Dictionary<string, string> payload)
            {
                throw new NotSupportedException();
            }

            public override Task<InvocationState> Enqueue(string job, string source)
            {
                throw new NotSupportedException();
            }

            public override Task<InvocationState> Enqueue(string job, string source, Dictionary<string, string> payload, TimeSpan invisibleFor)
            {
                throw new NotSupportedException();
            }

            public override Task<bool> Extend(InvocationState invocation, TimeSpan duration)
            {
                return Task.FromResult(true);
            }

            public override Task<InvocationState> Get(Guid id)
            {
                return Task.FromResult<InvocationState>(null);
            }

            public override Task<IEnumerable<InvocationState>> GetAll(InvocationListCriteria criteria)
            {
                return Task.FromResult<IEnumerable<InvocationState>>(Enumerable.Empty<InvocationState>());
            }

            public override Task<IEnumerable<InvocationState>> GetAll(InvocationListCriteria criteria, int? limit)
            {
                return Task.FromResult<IEnumerable<InvocationState>>(Enumerable.Empty<InvocationState>());
            }

            public override Task<IEnumerable<InvocationStatisticsRecord>> GetInstanceStatistics()
            {
                return Task.FromResult<IEnumerable<InvocationStatisticsRecord>>(Enumerable.Empty<InvocationStatisticsRecord>());
            }

            public override Task<IEnumerable<InvocationStatisticsRecord>> GetJobStatistics()
            {
                return Task.FromResult<IEnumerable<InvocationStatisticsRecord>>(Enumerable.Empty<InvocationStatisticsRecord>());
            }

            public override Task<IEnumerable<InvocationState>> GetPurgable()
            {
                return Task.FromResult<IEnumerable<InvocationState>>(Enumerable.Empty<InvocationState>());
            }

            public override Task<IEnumerable<InvocationState>> GetPurgable(DateTimeOffset before)
            {
                return Task.FromResult<IEnumerable<InvocationState>>(Enumerable.Empty<InvocationState>());
            }

            public override Task<InvocationStatisticsRecord> GetStatistics()
            {
                return Task.FromResult<InvocationStatisticsRecord>(null);
            }

            public override Task Purge(Guid id)
            {
                return Task.FromResult<object>(null);
            }

            public override Task Purge(IEnumerable<Guid> ids)
            {
                return Task.FromResult<object>(null);
            }

            public override Task<bool> Suspend(InvocationState invocation, Dictionary<string, string> newPayload, TimeSpan suspendFor, string logUrl)
            {
                return Task.FromResult(true);
            }

            public override Task<bool> UpdateStatus(InvocationState invocation, InvocationStatus status, ExecutionResult result)
            {
                return Task.FromResult(true);
            }
        }
    }
}
