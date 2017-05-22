// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing
{
    [DebuggerDisplay("on:{BoostByDownloads} thd:{Threshold} factor:{Factor}")]
    public class QueryBoostingContext
    {
        public QueryBoostingContext(
            bool boostByDownloads,
            int threshold,
            float factor)
        {
            BoostByDownloads = boostByDownloads;
            Threshold = Math.Max(1, threshold);
            Factor = Math.Max(0.1f, factor);
        }

        public static QueryBoostingContext Default { get; } = new QueryBoostingContext(true, 1000, 20.0f);

        /// <summary>
        /// Should boosting by download count be run on each query.
        /// </summary>
        public bool BoostByDownloads { get; }

        /// <summary>
        /// The minimal download count per package that makes it participate in boosting
        /// </summary>
        public int Threshold { get; }

        /// <summary>
        /// The boosting factor, the smaller it is, the higher the logarithmic boost per package.
        /// </summary>
        public float Factor { get; }

        public static QueryBoostingContext Load(string fileName, ILoader loader, FrameworkLogger logger)
        {
            try
            {
                using (var reader = loader.GetReader(fileName))
                {
                    var serializer = new JsonSerializer();

                    var value = serializer.Deserialize<QueryBoostingContext>(reader);

                    return value ?? throw new InvalidOperationException("Failed to deserialize the query boosting context");
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Unable to load {FileName} as deserialization threw: {Exception}", fileName, ex);

                throw;
            }
        }
    }
}
