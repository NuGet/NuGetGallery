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
    public interface ICommandExecutor
    {
        TResult Execute<TResult>(Command<TResult> command);
    }

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
    public class CommandExecutor : ICommandExecutor
    {
        public IServiceProvider Container { get; protected set; }
        public IDiagnosticsSource Trace { get; protected set; }

        public CommandExecutor(IServiceProvider container, IDiagnosticsService diagnostics)
        {
            Trace = diagnostics.GetSource("CommandExecutor");
            Container = container;
        }

        public virtual TResult Execute<TResult>(Command<TResult> command)
        {
            Debug.Assert(command != null);

            // Get the handler
            var handlerType = typeof(CommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
            var handler = (ICommandHandler)Container.GetService(handlerType);
            
            using (Trace.Activity(String.Format(CultureInfo.InvariantCulture, "Execution of {0}", command.GetType().Name)))
            {
                return (TResult)handler.Execute(command);
            }
        }
    }

    public static class CommandExecutorExtensions
    {
        public static TResult ExecuteAndCatch<TResult>(this ICommandExecutor self, Command<TResult> command)
        {
            TResult result;
            try
            {
                result = self.Execute(command);
            }
            catch (Exception ex)
            {
                QuietLog.LogHandledException(ex);
                result = default(TResult);
            }
            return result;
        }

        public static async Task<TResult> ExecuteAndCatchAsync<TResult>(this ICommandExecutor self, Command<Task<TResult>> command)
        {
            TResult result;
            try
            {
                result = await self.Execute(command);
            }
            catch (Exception ex)
            {
                QuietLog.LogHandledException(ex);
                result = default(TResult);
            }
            return result;
        }

        public static Task<TResult[]> ExecuteAsyncAll<TResult>(this ICommandExecutor self, params Command<Task<TResult>>[] commands)
        {
            return Task.WhenAll(commands.Select(q => self.Execute(q)));
        }

        public static Task<TResult[]> ExecuteAndCatchAsyncAll<TResult>(this ICommandExecutor self, params Command<Task<TResult>>[] commands)
        {
            return Task.WhenAll(commands.Select(q => ExecuteAndCatchAsync(self, q)));
        }
    }
}