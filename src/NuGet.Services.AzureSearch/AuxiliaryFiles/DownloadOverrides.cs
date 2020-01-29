// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public static class DownloadOverrides
    {
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        public static IReadOnlyDictionary<string, long> Load(JsonReader reader, ILogger logger)
        {
            try
            {
                var downloadOverrides = Serializer.Deserialize<Dictionary<string, long>>(reader);

                return new Dictionary<string, long>(
                    downloadOverrides,
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                logger.LogError(0, ex, "Unable to load download overrides due to exception");
                throw;
            }
        }
    }
}
