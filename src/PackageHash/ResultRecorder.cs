// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.PackageHash
{
    public class ResultRecorder : IResultRecorder
    {
        private readonly ILogger<ResultRecorder> _logger;

        public ResultRecorder(ILogger<ResultRecorder> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RecordAsync(IReadOnlyList<InvalidPackageHash> results)
        {
            using (var fileStream = new FileStream("results.csv", FileMode.Append))
            using (var writer = new StreamWriter(fileStream))
            {
                if (fileStream.Position == 0)
                {
                    await writer.WriteLineAsync("Type,URL,ID,Version,ExpectedHash,ActualHash");
                }

                foreach (var result in results)
                {
                    _logger.LogError(
                        "Inconsistent hash found for {PackageId} {PackageVersion} on {SourceUrl} ({SourceType}). " +
                        "Expected {ExpectedHash}, found {ActualHash}.",
                        result.Package.Identity.Id,
                        result.Package.Identity.Version.ToNormalizedString(),
                        result.Source.Url,
                        result.Source.Type,
                        result.Package.ExpectedHash,
                        result.InvalidHash);

                    var pieces = new object[]
                    {
                        result.Source.Type,
                        result.Source.Url,
                        result.Package.Identity.Id,
                        result.Package.Identity.Version.ToNormalizedString(),
                        result.Package.ExpectedHash,
                        result.InvalidHash,
                    };

                    await writer.WriteLineAsync(string.Join(",", pieces));
                }
            }
        }
    }
}
