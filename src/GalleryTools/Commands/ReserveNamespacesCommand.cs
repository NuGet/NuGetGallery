// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery;

namespace GalleryTools.Commands
{
    public static class ReserveNamespacesCommand
    {
        private const int DefaultSleepSeconds = 0;
        private const string PathOption = "--path";

        public static void Configure(CommandLineApplication config)
        {
            config.Description = "Bulk reserve namespaces";
            config.HelpOption("-? | -h | --help");

            var pathOptions = config.Option(
                $"-p | {PathOption}",
                "A path to a simple text file, which is the list of namespaces to reserve. One namespace per line. Prefix namespaces should end with '*', otherwise they will be exact match reservations.",
                CommandOptionType.SingleValue);

            var sleepDurationOption = config.Option(
                "-s | --sleep",
                $"The duration in seconds to sleep between each reservation (default: {DefaultSleepSeconds}).",
                CommandOptionType.SingleValue);

            var unreserveOption = config.Option(
                "-u | --unreserve",
                "Unreserve namespaces, instead of reserving them. Meant for a rollback of bulk reserving. Note that you must clear the progress file to reuse the same input file.",
                CommandOptionType.NoValue);

            config.OnExecute(() =>
            {
                return ExecuteAsync(pathOptions, sleepDurationOption, unreserveOption).GetAwaiter().GetResult();
            });
        }

        private static async Task<int> ExecuteAsync(
            CommandOption pathOption,
            CommandOption sleepDurationOption,
            CommandOption unreserveOption)
        {
            if (!pathOption.HasValue())
            {
                Console.WriteLine($"The '{PathOption}' parameter is required.");
                return 1;
            }

            var sleepDuration = TimeSpan.FromSeconds(DefaultSleepSeconds);
            if (sleepDurationOption.HasValue())
            {
                sleepDuration = TimeSpan.FromSeconds(int.Parse(sleepDurationOption.Value()));

                if (sleepDuration < TimeSpan.Zero)
                {
                    Console.WriteLine("The sleep duration must be zero or more seconds.");
                    return 1;
                }
            }

            var unreserve = unreserveOption.HasValue();

            var path = pathOption.Value();
            var completedPath = path + ".progress";
            var remainingList = GetRemainingList(path, completedPath);
            Console.WriteLine($"{remainingList.Count} reserved namespace(s) to {(unreserve ? "remove" : "add")}.");
            if (!remainingList.Any())
            {
                Console.WriteLine("No namespaces were found to reserve.");
                return 1;
            }

            var builder = new ContainerBuilder();
            builder.RegisterType<ReservedNamespaceService>().AsSelf();
            builder.RegisterAssemblyModules(typeof(DefaultDependenciesModule).Assembly);
            var container = builder.Build();
            var service = container.Resolve<ReservedNamespaceService>();

            var totalCounter = 0;
            foreach (var reservedNamespace in remainingList)
            {
                Console.Write($"{(unreserve ? "Removing" : "Adding")} '{reservedNamespace.Value}' IsPrefix = {reservedNamespace.IsPrefix}...");
                try
                {
                    var matching = service
                        .FindReservedNamespacesForPrefixList(new[] { reservedNamespace.Value })
                        .SingleOrDefault(x => ReservedNamespaceComparer.Instance.Equals(x, reservedNamespace));

                    if (unreserve)
                    {
                        if (matching == null)
                        {
                            Console.WriteLine(" does not exist.");
                            AppendReservedNamespace(completedPath, reservedNamespace);
                            continue;
                        }

                        await service.DeleteReservedNamespaceAsync(reservedNamespace.Value);
                    }
                    else
                    {
                        if (matching != null)
                        {
                            Console.WriteLine(" already exists.");
                            AppendReservedNamespace(completedPath, reservedNamespace);
                            continue;
                        }

                        await service.AddReservedNamespaceAsync(reservedNamespace);
                    }

                    AppendReservedNamespace(completedPath, reservedNamespace);
                    totalCounter++;
                    Console.WriteLine(" done.");
                }
                catch (Exception e)
                {
                    Console.WriteLine(" error!");
                    Console.WriteLine(e);
                }

                if (sleepDuration > TimeSpan.Zero)
                {
                    Console.WriteLine($"Sleeping for {sleepDuration}.");
                    await Task.Delay(sleepDuration);
                }
            }

            Console.WriteLine($"All done. {(unreserve ? "Removed" : "Added")} {totalCounter} reserved namespace(s).");

            return 0;
        }

        private static List<ReservedNamespace> GetRemainingList(string path, string completedPath)
        {
            Console.WriteLine($"Reading reserved namespaces from {path}...");
            var all = ReadReservedNamespaces(path);

            var completed = new List<ReservedNamespace>();
            if (File.Exists(completedPath))
            {
                Console.WriteLine($"Reading completed reserved namespaces from {completedPath}...");
                completed.AddRange(ReadReservedNamespaces(completedPath));
            }

            var remaining = all.Except(completed, ReservedNamespaceComparer.Instance).ToList();
            if (remaining.Count != all.Count)
            {
                Console.WriteLine($"{all.Count - remaining.Count} reserved namespaces(s) are already done.");
            }

            return remaining;
        }

        private static void AppendReservedNamespace(string completedListPath, ReservedNamespace reservedNamespace)
        {
            using (var fileStream = new FileStream(completedListPath, FileMode.Append, FileAccess.Write))
            using (var writer = new StreamWriter(fileStream))
            {
                writer.WriteLine($"{reservedNamespace.Value}{(reservedNamespace.IsPrefix ? "*" : string.Empty)}");
            }
        }

        private static List<ReservedNamespace> ReadReservedNamespaces(string path)
        {
            var uniqueReservedNamespaces = new HashSet<ReservedNamespace>(new ReservedNamespaceComparer());
            var output = new List<ReservedNamespace>();
            int lineNumber = 0;
            string line;
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(fileStream))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var value = line.Trim();
                    var isPrefix = false;
                    if (line.EndsWith("*"))
                    {
                        value = value.Substring(0, value.Length - 1);
                        isPrefix = true;
                    }

                    // Ensure the reserved namespace is actually a valid package ID.
                    var validatedPrefix = value;
                    if (isPrefix)
                    {
                        // Prefix reserved namespaces can end with '-' and '.'. Package IDs cannot.
                        if (value.EndsWith("-") || value.EndsWith("."))
                        {
                            validatedPrefix = validatedPrefix.Substring(0, validatedPrefix.Length - 1);
                        }
                    }

                    if (!PackageIdValidator.IsValidPackageId(validatedPrefix))
                    {
                        Console.WriteLine($"Line {lineNumber}: Ignoring invalid reserved namespace (validated: '{validatedPrefix}', original: '{line}').");
                        continue;
                    }

                    var reservedNamespace = new ReservedNamespace
                    {
                        Value = value,
                        IsPrefix = isPrefix,
                        IsSharedNamespace = false,
                    };

                    if (!uniqueReservedNamespaces.Add(reservedNamespace))
                    {
                        Console.WriteLine($"Line {lineNumber}: Ignoring duplicate reserved namespace.");
                        continue;
                    }

                    output.Add(reservedNamespace);
                }
            }

            return output;
        }

        private class ReservedNamespaceComparer : IEqualityComparer<ReservedNamespace>
        {
            public static ReservedNamespaceComparer Instance { get; } = new ReservedNamespaceComparer();

            public bool Equals(ReservedNamespace x, ReservedNamespace y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return StringComparer.OrdinalIgnoreCase.Equals(x.Value, y.Value)
                    && x.IsPrefix == y.IsPrefix;
            }

            public int GetHashCode(ReservedNamespace obj)
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Value) ^ obj.IsPrefix.GetHashCode();
            }
        }
    }
}
