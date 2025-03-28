// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Common;
using Xunit.Abstractions;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    internal sealed class TestLogger : ILogger
    {
        private readonly ITestOutputHelper _output;

        internal TestLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public void LogDebug(string data)
        {
            DumpMessage("DEBUG", data);
        }

        public void LogError(string data)
        {
            DumpMessage("ERROR", data);
        }

        public void LogInformation(string data)
        {
            DumpMessage("INFO ", data);
        }

        public void LogMinimal(string data)
        {
            DumpMessage("LOG  ", data);
        }

        public void LogVerbose(string data)
        {
            DumpMessage("TRACE", data);
        }

        public void LogWarning(string data)
        {
            DumpMessage("WARN ", data);
        }

        public void LogInformationSummary(string data)
        {
            DumpMessage("ISMRY", data);
        }

        private void DumpMessage(string level, string data)
        {
            _output.WriteLine($"{level}: {data}");
        }

        public void Log(LogLevel level, string data)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    {
                        LogDebug(data);
                        break;
                    }

                case LogLevel.Error:
                    {
                        LogError(data);
                        break;
                    }

                case LogLevel.Information:
                    {
                        LogInformation(data);
                        break;
                    }

                case LogLevel.Minimal:
                    {
                        LogMinimal(data);
                        break;
                    }

                case LogLevel.Verbose:
                    {
                        LogVerbose(data);
                        break;
                    }

                case LogLevel.Warning:
                    {
                        LogWarning(data);
                        break;
                    }
            }
        }

        public Task LogAsync(LogLevel level, string data)
        {
            Log(level, data);

            return Task.CompletedTask;
        }

        public void Log(ILogMessage message)
        {
            Log(message.Level, message.Message);
        }

        public async Task LogAsync(ILogMessage message)
        {
            await LogAsync(message.Level, message.FormatWithCode());
        }
    }
}
