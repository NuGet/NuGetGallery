using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        protected CommandExecutor() { }
        public CommandExecutor(IDiagnosticsService diagnostics)
        {
            Trace = diagnostics.GetSource("CommandExecutor");
        }

        public virtual async Task<object> Execute(IQuery query)
        {
            Debug.Assert(query != null);

            using (Trace.Activity(String.Format("Execution of {0}", query.GetType().Name)))
            {
                return await query.Execute();
            }
        }

        public virtual async Task Execute(ICommand command)
        {
            Debug.Assert(command != null);

            using (Trace.Activity(String.Format("Execution of {0}", command.GetType().Name)))
            {
                await command.Execute();
            }
        }

        public virtual async Task<TResult> Execute<TResult>(Query<TResult> query)
        {
            var result = await Execute((IQuery)query);
            return (TResult)(result ?? default(TResult));
        }
    }
}