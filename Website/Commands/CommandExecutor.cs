using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Commands
{
    /// <summary>
    /// Executes commands and queries against the service layer.
    /// </summary>
    /// <remarks>
    /// Why have an abstraction for a simple method call? Tests can easily mock this out with very little ceremony like so:
    /// <code>
    /// var mockExecutor = new Mock&lt;ICommandExecutor&gt;();
    /// mockExecutor.Setup(e => e.Execute(new GetVersionsOfPackageQuery("jQuery"))).Returns(...); 
    /// </code>
    /// 
    /// Also, common tasks like tracing and profiling can be inserted here.
    /// </remarks>
    public class CommandExecutor
    {
        public IDiagnosticsSource Trace { get; protected set; }

        protected CommandExecutor() 
        {
            Trace = new NullDiagnosticsSource();
        }

        public CommandExecutor(IDiagnosticsService diagnostics)
        {
            Trace = diagnostics.GetSource("CommandExecutor");
        }

        public virtual void Execute(Command command)
        {
            Debug.Assert(command != null);

            using (Trace.Activity(String.Format(CultureInfo.InvariantCulture, "Execution of {0}", command.GetType().Name)))
            {
                command.Execute();
            }
        }

        public virtual TResult Execute<TResult>(Command<TResult> query)
        {
            Debug.Assert(query != null);

            using (Trace.Activity(String.Format(CultureInfo.InvariantCulture, "Execution of {0}", query.GetType().Name)))
            {
                return query.Execute();
            }
        }

        public TResult ExecuteAndCatch<TResult>(Command<TResult> query)
        {
            TResult result;
            try
            {
                result = Execute(query);
            }
            catch (Exception ex)
            {
                QuietLog.LogHandledException(ex);
                result = default(TResult);
            }
            return result;
        }

        public async Task<TResult> ExecuteAndCatchAsync<TResult>(Command<Task<TResult>> query)
        {
            TResult result;
            try
            {
                result = await Execute(query);
            }
            catch (Exception ex)
            {
                QuietLog.LogHandledException(ex);
                result = default(TResult);
            }
            return result;
        }

        public virtual Task<TResult[]> ExecuteAsyncAll<TResult>(params Command<Task<TResult>>[] queries)
        {
            return Task.WhenAll(queries.Select(q => Execute(q)));
        }

        public virtual Task<TResult[]> ExecuteAndCatchAsyncAll<TResult>(params Command<Task<TResult>>[] queries)
        {
            return Task.WhenAll(queries.Select(q => ExecuteAndCatchAsync(q)));
        }
    }
}