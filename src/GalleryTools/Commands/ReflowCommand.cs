// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGetGallery;

namespace GalleryTools.Commands
{
    public static class ReflowCommand
    {
        private const int DefaultBatch = 1;
        private const int DefaultSleepMultiplier = 60;
        private const string PackageListOption = "--path";

        public static void Configure(CommandLineApplication config)
        {
            config.Description = "Bulk reflow many packages";
            config.HelpOption("-? | -h | --help");

            var packageListOption = config.Option(
                $"-p | {PackageListOption}",
                "A path to a list of packages, one ID and version per line with a space in between.",
                CommandOptionType.SingleValue);

            var batchSizeOption = config.Option(
                "-b | --batch",
                $"The number of packages to reflow in a batch (default: {DefaultBatch}).",
                CommandOptionType.SingleValue);

            var sleepDurationOption = config.Option(
                "-s | --sleep",
                $"The duration in seconds to sleep between each batch (default: batch size * {DefaultSleepMultiplier}).",
                CommandOptionType.SingleValue);

            config.OnExecute(() =>
            {
                return ExecuteAsync(packageListOption, batchSizeOption, sleepDurationOption).GetAwaiter().GetResult();
            });
        }

        private static async Task<int> ExecuteAsync(
            CommandOption packageListOption,
            CommandOption batchSizeOption,
            CommandOption sleepDurationOption)
        {
            if (!packageListOption.HasValue())
            {
                Console.WriteLine($"The '{PackageListOption}' parameter is required.");
                return 1;
            }

            var batchSize = DefaultBatch;
            if (batchSizeOption.HasValue())
            {
                batchSize = int.Parse(batchSizeOption.Value());

                if (batchSize <= 0)
                {
                    Console.WriteLine("The batch size must be greater than zero.");
                    return 1;
                }
            }

            var sleepDuration = TimeSpan.FromSeconds(batchSize * DefaultSleepMultiplier);
            if (sleepDurationOption.HasValue())
            {
                sleepDuration = TimeSpan.FromSeconds(int.Parse(sleepDurationOption.Value()));

                if (sleepDuration < TimeSpan.Zero)
                {
                    Console.WriteLine("The sleep duration but be zero or more seconds.");
                    return 1;
                }
            }

            var packageListPath = packageListOption.Value();
            var completedListPath = packageListPath + ".progress";
            var remainingList = GetRemainingList(packageListPath, completedListPath);
            if (!remainingList.Any())
            {
                return 1;
            }

            var builder = new ContainerBuilder();
            builder.RegisterType<ReflowPackageService>().AsSelf();
            builder.RegisterAssemblyModules(typeof(DefaultDependenciesModule).Assembly);
            var container = builder.Build();
            var reflowPackageService = container.Resolve<ReflowPackageService>();

            var batchCounter = 0;
            var totalCounter = 0;
            for (var i = 0; i < remainingList.Count; i++)
            {
                var identity = remainingList[i];
                var version = identity.Version.ToNormalizedString();
                Console.Write($"Reflowing {identity.Id} {version}...");
                try
                {
                    var package = await reflowPackageService.ReflowAsync(identity.Id, version);
                    if (package != null)
                    {
                        Console.WriteLine(" done.");
                        AppendPackage(completedListPath, package.PackageRegistration.Id, package.NormalizedVersion);
                        batchCounter++;
                        totalCounter++;
                    }
                    else
                    {
                        Console.WriteLine(" not found.");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(" error!");
                    Console.WriteLine(e);
                }

                if (batchCounter >= batchSize
                    && i < remainingList.Count - 1
                    && sleepDuration > TimeSpan.Zero)
                {
                    Console.WriteLine($"Completed a batch. Sleeping for {sleepDuration}.");
                    await Task.Delay(sleepDuration);
                    batchCounter = 0;
                }
            }

            Console.WriteLine($"All done. Reflowed {totalCounter} package(s).");

            return 0;
        }

        private static List<PackageIdentity> GetRemainingList(string packageListPath, string completedListPath)
        {
            Console.WriteLine($"Reading packages from {packageListPath}...");
            var packageList = ReadPackageList(packageListPath);

            var completedList = new List<PackageIdentity>();
            if (File.Exists(completedListPath))
            {
                Console.WriteLine($"Reading completed packages from {completedListPath}...");
                completedList.AddRange(ReadPackageList(completedListPath));
            }

            var remainingList = packageList.Except(completedList).ToList();
            Console.WriteLine($"{remainingList.Count} package(s) to reflow.");

            return remainingList;
        }

        private static void AppendPackage(string completedListPath, string id, string version)
        {
            using (var fileStream = new FileStream(completedListPath, FileMode.Append, FileAccess.Write))
            using (var writer = new StreamWriter(fileStream))
            {
                writer.WriteLine($"{id} {version}");
            }
        }

        private static List<PackageIdentity> ReadPackageList(string path)
        {
            var uniquePackageIdentities = new HashSet<PackageIdentity>();
            var output = new List<PackageIdentity>();
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

                    var pieces = line.Split(new[] { ' ' }, 2);
                    if (pieces.Length != 2)
                    {
                        Console.WriteLine($"Line {lineNumber}: Ignoring line without a space separator.");
                        continue;
                    }

                    var id = pieces[0].Trim();
                    var unparsedVersion = pieces[1].Trim();

                    if (string.IsNullOrWhiteSpace(id))
                    {
                        Console.WriteLine($"Line {lineNumber}: Ignoring empty ID.");
                        continue;
                    }

                    NuGetVersion version;
                    if (string.IsNullOrWhiteSpace(unparsedVersion) || !NuGetVersion.TryParse(unparsedVersion, out version))
                    {
                        Console.WriteLine($"Line {lineNumber}: Ignoring invalid version.");
                        continue;
                    }

                    var packageIdentity = new PackageIdentity(id, version);
                    if (!uniquePackageIdentities.Add(packageIdentity))
                    {
                        Console.WriteLine($"Line {lineNumber}: Ignoring duplicate package.");
                        continue;
                    }

                    output.Add(packageIdentity);
                }
            }

            return output;
        }
    }
}
