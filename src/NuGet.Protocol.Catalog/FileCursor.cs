// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace NuGet.Protocol.Catalog
{
    /// <summary>
    /// A cursor implementation which stores the cursor in local file. The cursor value is written to the file as
    /// a JSON object.
    /// </summary>
    public class FileCursor : ICursor
    {
        private static readonly JsonSerializerSettings Settings = NuGetJsonSerialization.Settings;
        private readonly string _path;
        private readonly ILogger<FileCursor> _logger;

        public FileCursor(string path, ILogger<FileCursor> logger)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<DateTimeOffset?> GetAsync()
        {
            try
            {
                var jsonString = File.ReadAllText(_path);
                var data = JsonConvert.DeserializeObject<Data>(jsonString, Settings);
                _logger.LogDebug("Read cursor value {cursor:O} from {path}.", data.Value, _path);
                return Task.FromResult<DateTimeOffset?>(data.Value);
            }
            catch (Exception e) when (e is FileNotFoundException || e is JsonException)
            {
                return Task.FromResult<DateTimeOffset?>(null);
            }
        }

        public Task SetAsync(DateTimeOffset value)
        {
            var data = new Data { Value = value };
            var jsonString = JsonConvert.SerializeObject(data);
            File.WriteAllText(_path, jsonString);
            _logger.LogDebug("Wrote cursor value {cursor:O} to {path}.", data.Value, _path);
            return Task.CompletedTask;
        }

        private class Data
        {
            [JsonProperty("value")]
            public DateTimeOffset Value { get; set; }
        }
    }
}
