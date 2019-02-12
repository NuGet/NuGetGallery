// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
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
                MissingMemberHandling = MissingMemberHandling.Error
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

        public async Task<FeatureFlagSaveResult> TrySaveAsync(string flags, string contentId)
        {
            // Ensure the feature flags are valid before saving them.
            var validationResult = ValidateFlagsOrNull(flags);
            if (validationResult != null)
            {
                return validationResult;
            }

            var accessCondition = AccessConditionWrapper.GenerateIfMatchCondition(contentId);

            try
            {
                using (var stream = new MemoryStream())
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(PrettifyJson(flags));
                    writer.Flush();
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

        public async Task<bool> TryRemoveUserAsync(User user)
        {
            for (var attempt = 0; attempt < MaxRemoveUserAttempts; attempt++)
            {
                try
                {
                    await MaybeUpdateFlagsAsync((FeatureFlags flags) =>
                    {
                        // Don't update the flags if the user isn't listed in any of the flights.
                        if (!flags.Flights.Any(f => f.Value.Accounts.Contains(user.Username, StringComparer.OrdinalIgnoreCase)))
                        {
                            return null;
                        }

                        // The user is listed in the flights. Build a new feature flag object that
                        // no longer contains the user.
                         return new FeatureFlags(
                            flags.Features,
                            flags.Flights
                                .ToDictionary(
                                    f => f.Key,
                                    f => RemoveUser(f.Value, user)));
                    });

                    return true;
                }
                catch (StorageException e) when (e.IsPreconditionFailedException())
                {
                    _logger.LogWarning(
                        0,
                        e,
                        "Failed to remove user from feature flags due to precondition failed exception, " +
                        "attempt {Attempt} of {MaxAttempts}...",
                        attempt + 1,
                        MaxRemoveUserAttempts);
                }
            }

            return false;
        }

        private Flight RemoveUser(Flight flight, User user)
        {
            return new Flight(
                flight.All,
                flight.SiteAdmins,
                flight.Accounts.Where(a => !a.Equals(user.Username, StringComparison.OrdinalIgnoreCase)).ToList(),
                flight.Domains);
        }

        private async Task MaybeUpdateFlagsAsync(Func<FeatureFlags, FeatureFlags> updateAction)
        {
            var reference = await _storage.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName);

            FeatureFlags flags;
            using (var stream = reference.OpenRead())
            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {
                flags = _serializer.Deserialize<FeatureFlags>(reader);
            }

            // The action can cancel the update by returning null.
            var result = updateAction(flags);
            if (result == null)
            {
                return;
            }

            var accessCondition = AccessConditionWrapper.GenerateIfMatchCondition(reference.ContentId);

            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                _serializer.Serialize(jsonWriter, result);
                jsonWriter.Flush();
                stream.Position = 0;

                await _storage.SaveFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, stream, accessCondition);
            }
        }

        private FeatureFlagSaveResult ValidateFlagsOrNull(string flags)
        {
            try
            {
                using (var reader = new StringReader(flags))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    _serializer.Deserialize<FeatureFlags>(jsonReader);
                    return null;
                }
            }
            catch (JsonException e)
            {
                return FeatureFlagSaveResult.Invalid(e.Message);
            }
        }

        private string PrettifyJson(string json)
        {
            return JToken.Parse(json).ToString(Formatting.Indented);
        }
    }
}
