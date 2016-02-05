// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;

namespace NuGet.Services
{
    internal static class Logging
    {
        public static LoggerConfiguration CreateDefaultLoggerConfiguration()
        {
            return new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.ColoredConsole()
                .Enrich.WithMachineName();
        }

        public static ILoggerFactory CreateLoggerFactory(
            LoggerConfiguration loggerConfiguration = null,
            string elasticsearchEndpoint = null,
            string elasticsearchUsername = null,
            string elasticsearchPassword = null)
        {
            // setup Serilog
            if (loggerConfiguration == null)
            {
                loggerConfiguration = CreateDefaultLoggerConfiguration();
            }

            if (string.IsNullOrEmpty(elasticsearchEndpoint))
            {
                // fallback to config setting if available
                elasticsearchEndpoint = ConfigurationManager.AppSettings["serilog:Elasticsearch.nodeUris"];
            }

            if (!string.IsNullOrEmpty(elasticsearchEndpoint))
            {
                var nodeUris = new List<Uri>();
                foreach (var nodeUriValue in elasticsearchEndpoint.Split(';'))
                {
                    Uri nodeUri;
                    if (Uri.TryCreate(nodeUriValue, UriKind.Absolute, out nodeUri)
                        && !nodeUris.Contains(nodeUri))
                    {
                        nodeUris.Add(nodeUri);
                    }
                }

                var elasticsearchOptions = new ElasticsearchSinkOptions(nodeUris);
                elasticsearchOptions.AutoRegisterTemplate = true;

                // authentication settings
                if (elasticsearchUsername == null)
                {
                    elasticsearchUsername = ConfigurationManager.AppSettings["serilog:Elasticsearch.username"];
                }

                if (elasticsearchPassword == null)
                {
                    elasticsearchPassword = ConfigurationManager.AppSettings["serilog:Elasticsearch.password"];
                }

                if (!string.IsNullOrEmpty(elasticsearchUsername)
                    && !string.IsNullOrEmpty(elasticsearchPassword))
                {
                    elasticsearchOptions.ModifyConnectionSettings =
                        config => config.SetBasicAuthentication(elasticsearchUsername, elasticsearchPassword);
                }

                loggerConfiguration = loggerConfiguration
                    .WriteTo.Elasticsearch(elasticsearchOptions);
            }

            Log.Logger = loggerConfiguration.CreateLogger();

            // hook-up Serilog to Microsoft.Extensions.Logging
            var loggerFactory = new LoggerFactory();

            // note: this confusing setting will be removed when new version of Microsoft.Extensions.Logging is out
            // https://github.com/aspnet/Announcements/issues/122
            loggerFactory.MinimumLevel = LogLevel.Debug;
            loggerFactory.AddProvider(new SerilogLoggerProvider());

            // hook into anything that is being traced in other libs using system.diagnostics.trace
            System.Diagnostics.Trace.Listeners.Add(new SerilogTraceListener.SerilogTraceListener());

            return loggerFactory;
        }

        // note: there is an existing SerilogLoggerProvider for Microsoft.Extensions.Logging which requires DNX
        // see also: Serilog.Framework.Logging package on nuget.org
        private class SerilogLoggerProvider
            : ILoggerProvider
        {
            private const string _originalFormatPropertyName = "{OriginalFormat}";

            public void Dispose()
            {
            }

            public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
            {
                return new SerilogLogger(this, categoryName);
            }

            private IDisposable BeginScopeImpl(string categoryName, object state)
            {
                return new SerilogLoggerScope(this, categoryName, state);
            }

            private readonly string _currentScopeKey = nameof(SerilogLoggerScope) + "#" + Guid.NewGuid().ToString("n");

            private SerilogLoggerScope CurrentScope
            {
                get
                {
                    var objectHandle = CallContext.LogicalGetData(_currentScopeKey) as ObjectHandle;
                    return objectHandle != null ? objectHandle.Unwrap() as SerilogLoggerScope : null;
                }
                set
                {
                    CallContext.LogicalSetData(_currentScopeKey, new ObjectHandle(value));
                }
            }

            private class SerilogLogger
                : Microsoft.Extensions.Logging.ILogger
            {
                private readonly string _categoryName;
                private readonly SerilogLoggerProvider _provider;
                private readonly Serilog.ILogger _logger;

                public SerilogLogger(SerilogLoggerProvider provider, string categoryName)
                {
                    if (provider == null)
                    {
                        throw new ArgumentNullException(nameof(provider));
                    }

                    _provider = provider;
                    _categoryName = categoryName;

                    if (categoryName != null)
                    {
                        _logger = Serilog.Log.Logger.ForContext(Serilog.Core.Constants.SourceContextPropertyName, categoryName);
                    }
                    else
                    {
                        _logger = Serilog.Log.Logger;
                    }
                }

                public void Log(LogLevel logLevel, int eventId, object state, Exception exception,
                    Func<object, Exception, string> formatter)
                {
                    var level = ConvertLevel(logLevel);
                    if (!_logger.IsEnabled(level))
                    {
                        return;
                    }

                    var logger = _logger;
                    string messageTemplate = null;

                    var structure = state as ILogValues;
                    if (structure != null)
                    {
                        foreach (var property in structure.GetValues())
                        {
                            if (property.Key == SerilogLoggerProvider._originalFormatPropertyName &&
                                property.Value is string)
                            {
                                messageTemplate = (string)property.Value;
                            }
                            else if (property.Key.StartsWith("@"))
                            {
                                logger = logger.ForContext(property.Key.Substring(1), property.Value,
                                    destructureObjects: true);
                            }
                            else
                            {
                                logger = logger.ForContext(property.Key, property.Value);
                            }
                        }

                        var stateType = state.GetType();
                        var stateTypeInfo = stateType.GetTypeInfo();

                        // Imperfect, but at least eliminates `1 and + names
                        if (messageTemplate == null && !stateTypeInfo.IsGenericType && !stateTypeInfo.IsNested)
                        {
                            messageTemplate = "{" + stateType.Name + ":l}";
                            logger = logger.ForContext(stateType.Name, LogFormatter.Formatter(state, null));
                        }
                    }

                    if (messageTemplate == null && state != null)
                    {
                        messageTemplate = LogFormatter.Formatter(state, null);
                    }

                    if (string.IsNullOrEmpty(messageTemplate))
                    {
                        return;
                    }

                    if (eventId != 0)
                    {
                        logger = logger.ForContext("EventId", eventId);
                    }

                    logger.Write(level, exception, messageTemplate);
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    return _logger.IsEnabled(ConvertLevel(logLevel));
                }

                public IDisposable BeginScopeImpl(object state)
                {
                    return _provider.BeginScopeImpl(_categoryName, state);
                }

                private static LogEventLevel ConvertLevel(LogLevel logLevel)
                {
                    switch (logLevel)
                    {
                        case LogLevel.Critical:
                            return LogEventLevel.Fatal;
                        case LogLevel.Error:
                            return LogEventLevel.Error;
                        case LogLevel.Warning:
                            return LogEventLevel.Warning;
                        case LogLevel.Information:
                            return LogEventLevel.Information;
                        case LogLevel.Verbose:
                            return LogEventLevel.Debug;
                        default:
                            return LogEventLevel.Verbose;
                    }
                }
            }

            private class SerilogLoggerScope
                : IDisposable
            {
                private readonly SerilogLoggerProvider _provider;

                public SerilogLoggerScope(SerilogLoggerProvider provider, string name, object state)
                {
                    _provider = provider;
                    Name = name;
                    State = state;

                    Parent = _provider.CurrentScope;
                    _provider.CurrentScope = this;
                }

                private SerilogLoggerScope Parent { get; set; }
                private string Name { get; set; }
                private object State { get; set; }

                private void RemoveScope()
                {
                    for (var scan = _provider.CurrentScope; scan != null; scan = scan.Parent)
                    {
                        if (ReferenceEquals(scan, this))
                        {
                            _provider.CurrentScope = Parent;
                        }
                    }
                }

                private bool _disposedValue; // To detect redundant calls

                public void Dispose()
                {
                    if (!_disposedValue)
                    {
                        RemoveScope();
                    }
                    _disposedValue = true;
                }
            }
        }
    }
}