// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGet.Services.FeatureFlags;

namespace NuGetGallery.Features
{
    public class FeatureFlagFileStorageService : IEditableFeatureFlagStorageService
    {
        private const int MaxRemoveUserAttempts = 3;

        private readonly ICoreFileStorageService _storage;
        private readonly ILogger<FeatureFlagFileStorageService> _logger;
        private readonly JsonSerializer _serializer;

        public FeatureFlagFileStorageService(
            ICoreFileStorageService storage,
            ILogger<FeatureFlagFileStorageService> logger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                MissingMemberHandling = MissingMemberHandling.Error,
                Converters = new List<JsonConverter>
                {
                    new StringEnumConverter()
                }
            });
        }

        public async Task<FeatureFlags> GetAsync()
        {
            using (var stream = await _storage.GetFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName))
            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {
                return _serializer.Deserialize<FeatureFlags>(reader);
            }
        }

        public async Task<FeatureFlagReference> GetReferenceAsync()
        {
            var reference = await _storage.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName);

            string json;
            using (var stream = reference.OpenRead())
            using (var streamReader = new StreamReader(stream))
            {
                json = await streamReader.ReadToEndAsync();
            }

            return new FeatureFlagReference(
                PrettifyJson(json),
                reference.ContentId);
        }

        public async Task<FeatureFlagSaveResult> TrySaveAsync(string flagsJson, string contentId)
        {
            // Ensure the feature flags are valid before saving them.
            FeatureFlags flags;
            try
            {
                using (var reader = new StringReader(flagsJson))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    flags = _serializer.Deserialize<FeatureFlags>(jsonReader);
                }
            }
            catch (JsonException e)
            {
                return FeatureFlagSaveResult.Invalid(e.Message);
            }

            return await TrySaveAsync(flags, contentId);
        }

        public async Task<bool> TryRemoveUserAsync(User user)
        {
            for (var attempt = 0; attempt < MaxRemoveUserAttempts; attempt++)
            {
                var reference = await _storage.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName);

                FeatureFlags flags;
                using (var stream = reference.OpenRead())
                using (var streamReader = new StreamReader(stream))
                using (var reader = new JsonTextReader(streamReader))
                {
                    flags = _serializer.Deserialize<FeatureFlags>(reader);
                }

                // Don't update the flags if the user isn't listed in any of the flights.
                if (!flags.Flights.Any(f => f.Value.Accounts.Contains(user.Username, StringComparer.OrdinalIgnoreCase)))
                {
                    return true;
                }

                // The user is listed in the flights. Build a new feature flag object that
                // no longer contains the user.
                var result = new FeatureFlags(
                   flags.Features,
                   flags.Flights
                       .ToDictionary(
                           f => f.Key,
                           f => RemoveUser(f.Value, user)));

                var saveResult = await TrySaveAsync(result, reference.ContentId);
                if (saveResult.Type == FeatureFlagSaveResultType.Ok)
                {
                    return true;
                }

                _logger.LogWarning(
                    0,
                    "Failed to remove user from feature flags, attempt {Attempt} of {MaxAttempts}...",
                    attempt + 1,
                    MaxRemoveUserAttempts);
            }

            return false;
        }

        private async Task<FeatureFlagSaveResult> TrySaveAsync(FeatureFlags flags, string contentId)
        {
            var accessCondition = AccessConditionWrapper.GenerateIfMatchCondition(contentId);

            try
            {
                using (var stream = new MemoryStream())
                using (var writer = new StreamWriter(stream))
                using (var jsonWriter = new JsonTextWriter(writer))
                {
                    _serializer.Serialize(jsonWriter, flags);
                    jsonWriter.Flush();
                    stream.Position = 0;

                    await _storage.SaveFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, stream, accessCondition);

                    return FeatureFlagSaveResult.Ok;
                }
            }
            catch (StorageException e) when (e.IsPreconditionFailedException())
            {
                return FeatureFlagSaveResult.Conflict;
            }
        }

        private Flight RemoveUser(Flight flight, User user)
        {
            return new Flight(
                flight.All,
                flight.SiteAdmins,
                flight.Accounts.Where(a => !a.Equals(user.Username, StringComparison.OrdinalIgnoreCase)).ToList(),
                flight.Domains);
        }

        private string PrettifyJson(string json)
        {
            return JToken.Parse(json).ToString(Formatting.Indented);
        }
    }
}
