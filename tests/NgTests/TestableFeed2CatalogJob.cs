using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Persistence;
using Ng.Jobs;

namespace NgTests
{
    public class TestableFeed2CatalogJob
        : Feed2CatalogJob
    {
        private readonly HttpMessageHandler _handler;

        public TestableFeed2CatalogJob(HttpMessageHandler handler, string gallery, Storage catalogStorage, Storage auditingStorage, DateTime? startDate, TimeSpan timeout, int top, bool verbose)
            : base(new TestLoggerFactory())
        {
            _handler = handler;

            Gallery = gallery;
            CatalogStorage = catalogStorage;
            AuditingStorage = auditingStorage;
            StartDate = startDate;
            Timeout = timeout;
            Top = top;
            Verbose = verbose;
        }

        protected override HttpClient CreateHttpClient(bool verbose)
        {
            return new HttpClient(_handler);
        }

        public async Task RunOnce(CancellationToken cancellationToken)
        {
            await RunInternal(cancellationToken);
        }
    }

    internal class TestLoggerFactory
        : ILoggerFactory
    {
        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger();
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public LogLevel MinimumLevel { get; set; }
    }

    internal class TestLogger : ILogger
    {
        public void Log(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
        {
            Console.WriteLine($"{logLevel}: {formatter(state, exception)}");
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Console.WriteLine($"{logLevel}: {formatter(state, exception)}");
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return BeginScopeImpl(state);
        }

        public IDisposable BeginScopeImpl(object state)
        {
            return new TestLoggerScoper();
        }

        private class TestLoggerScoper : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}