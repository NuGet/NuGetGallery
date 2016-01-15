using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ng;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NgTests
{
    public class TestableFeed2Catalog
        : Feed2Catalog
    {
        private readonly HttpMessageHandler _handler;

        public TestableFeed2Catalog(HttpMessageHandler handler)
            : base(new TestLoggerFactory())
        {
            _handler = handler;
        }

        protected override HttpClient CreateHttpClient(bool verbose)
        {
            return new HttpClient(_handler);
        }

        public async Task InvokeProcessFeed(string gallery, Storage catalogStorage, Storage auditingStorage, DateTime? startDate, TimeSpan timeout, int top, bool verbose, CancellationToken cancellationToken)
        {
            await ProcessFeed(gallery, catalogStorage, auditingStorage, startDate, timeout, top, verbose, cancellationToken);
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

    internal class TestLogger
        : ILogger
    {
        public void Log(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
        {
            Console.WriteLine($"{logLevel}: {formatter(state, exception)}");
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScopeImpl(object state)
        {
            return new TestLoggerScoper();
        }

        private class TestLoggerScoper
            : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}