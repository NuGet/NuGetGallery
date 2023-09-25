// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Services.KeyVault;

namespace AzureSqlConnectionTest
{
    public class TestApplication : CommandLineApplication
    {
        public CommandOption Help { get; }

        public CommandOption KeyVaultName { get; }
        public CommandOption KeyVaultManaged { get; }
        public CommandOption KeyVaultTenantId { get; }

        public CommandOption KeyVaultClientId { get; }

        public CommandOption KeyVaultCertificateThumbprint { get; }

        public CommandOption ConnectionString { get; }

        public CommandOption Count { get; }

        public CommandOption PersistConnections { get; }

        public CommandOption DurationInSeconds { get; }

        public CommandOption IntervalInSeconds { get; }

        public CommandOption SpawnClients { get; }

        public CommandOption UseAdalOnly { get; }

        public TestApplication()
        {
            Help = HelpOption("-? | -h | --help");

            KeyVaultName = Option("-kv | --keyVaultName", "KeyVault name", CommandOptionType.SingleValue);
            KeyVaultManaged = Option("-kvm | --keyVaultUseMsi", "Use managed identity for KV authentication", CommandOptionType.NoValue);
            KeyVaultTenantId = Option("-kvtid | --keyVaultTenantId", "KeyVault tenant id", CommandOptionType.SingleValue);
            KeyVaultClientId = Option("-kvcid | --keyVaultClientId", "KeyVault client id", CommandOptionType.SingleValue);
            KeyVaultCertificateThumbprint = Option("-kvct | --keyVaultCertThumbprint", "KeyVault certificate thumbprint", CommandOptionType.SingleValue);

            ConnectionString = Option("-cs | --connectionString", "SQL connection string", CommandOptionType.SingleValue);

            Count = Option("-c | --count", "Client count", CommandOptionType.SingleValue);
            PersistConnections = Option("-p | --persist", "Persist connections", CommandOptionType.NoValue);
            DurationInSeconds = Option("-d | --duration", "Duration in seconds", CommandOptionType.SingleValue);
            IntervalInSeconds = Option("-i | --interval", "Sleep interval in seconds", CommandOptionType.SingleValue);

            SpawnClients = Option("-spawn", "Spawn client processes", CommandOptionType.NoValue);
            UseAdalOnly = Option("-adal", "Use MSAL only (default token cache)", CommandOptionType.NoValue);

            OnExecute(() => ExecuteAsync().GetAwaiter().GetResult());
        }

        public async Task<int> ExecuteAsync()
        {
            if (!Help.HasValue())
            {
                var count = IntValue(Count, defaultValue: 1);
                var durationInSeconds = IntValue(DurationInSeconds, defaultValue: 60);
                var intervalInSeconds = IntValue(IntervalInSeconds, defaultValue: 15);

                if (count > 1 && SpawnClients.HasValue())
                {
                    return await SpawnTestClients(count, durationInSeconds, intervalInSeconds);
                }

                var persist = PersistConnections.HasValue();
                var useAdalOnly = UseAdalOnly.HasValue();

                if (KeyVaultName.HasValue()
                    && ((KeyVaultClientId.HasValue()
                    && KeyVaultCertificateThumbprint.HasValue()) || KeyVaultManaged.HasValue()))
                {
                    using (var kvCertificate = KeyVaultManaged.HasValue() ? null : GetKeyVaultCertificate(KeyVaultCertificateThumbprint.Value()))
                    {
                        KeyVaultConfiguration keyVaultConfig;
                        if (KeyVaultManaged.HasValue())
                        {
                            keyVaultConfig = new KeyVaultConfiguration(KeyVaultName.Value());
                        }
                        else
                        {
                            keyVaultConfig = new KeyVaultConfiguration(
                                KeyVaultName.Value(),
                                KeyVaultTenantId.Value(),
                                KeyVaultClientId.Value(),
                                kvCertificate,
                                sendX5c: true);
                        }

                        var runner = new TestRunner(
                            ConnectionString.Value(),
                            keyVaultConfig);

                        return await runner.TestConnectionsAsync(
                            count,
                            durationInSeconds,
                            intervalInSeconds,
                            persist,
                            useAdalOnly);
                    }
                }
            }

            ShowHelp();

            return 1;
        }

        private async Task<int> SpawnTestClients(int count, int durationInSeconds, int intervalInSeconds)
        {
            var clients = new Task<int>[count];
            for (int i = 0; i < count; i++)
            {
                clients[i] = Task.Run(() => SpawnTestClient(durationInSeconds, intervalInSeconds));
            }

            await Task.WhenAll(clients);

            return clients.Count(c => c.Result != 0);
        }

        private int SpawnTestClient(int durationInSeconds, int intervalInSeconds)
        {
            var commandLine = GetTestClientCommandLine();

            var client = Process.Start(commandLine[0], commandLine[1]);
            if (!client.WaitForExit((durationInSeconds + (intervalInSeconds * 2)) * 1000))
            {
                client.Kill();
                return 1;
            }

            return client.ExitCode;
        }

        private static string[] GetTestClientCommandLine()
        {
            var commandLine = Environment.CommandLine;

            commandLine = Regex.Replace(commandLine,
                "-c(ount)? \\d+",
                "",
                RegexOptions.None,
                TimeSpan.FromSeconds(5));

            commandLine = Regex.Replace(commandLine,
                "-spawn",
                "",
                RegexOptions.None,
                TimeSpan.FromSeconds(5));

            var parts = commandLine.Split(' ');
            var arguments = parts
                .Skip(1)
                .Where(p => !String.IsNullOrEmpty(p));

            return new[] { parts[0], String.Join(" ", arguments) };
        }

        private static int IntValue(CommandOption option, int defaultValue = -1)
        {
            return option.HasValue() && Int32.TryParse(option.Value(), out var result)
                ? result
                : defaultValue;
        }

        private static X509Certificate2 GetKeyVaultCertificate(string kvCertThumbprint)
        {
            return CertificateUtility.FindCertificateByThumbprint(
                StoreName.My, StoreLocation.LocalMachine, kvCertThumbprint, validationRequired: true);
        }
    }
}
