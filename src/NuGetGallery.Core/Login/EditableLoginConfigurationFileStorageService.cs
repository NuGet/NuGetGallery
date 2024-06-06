// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGetGallery.Shared;

namespace NuGetGallery.Login
{
    public class EditableLoginConfigurationFileStorageService : LoginDiscontinuationFileStorageService, IEditableLoginConfigurationFileStorageService
    {
        private const int MaxAttempts = 3;
        private readonly ILogger<EditableLoginConfigurationFileStorageService> _logger;

        public EditableLoginConfigurationFileStorageService(
            ICoreFileStorageService storage,
            ILogger<EditableLoginConfigurationFileStorageService> logger) : base(storage)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<LoginDiscontinuationReference> GetReferenceAsync()
        {
            var reference = await _storage.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName);

            return new LoginDiscontinuationReference(
                ReadLoginDiscontinuationFromStream(reference.OpenRead()),
                reference.ContentId);
        }

        public async Task AddUserEmailAddressforPasswordAuthenticationAsync(string emailAddress, ContentOperations operation)
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var reference = await _storage.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName);

                LoginDiscontinuation logins;
                using (var stream = reference.OpenRead())
                using (var streamReader = new StreamReader(stream))
                using (var reader = new JsonTextReader(streamReader))
                {
                    logins = _serializer.Deserialize<LoginDiscontinuation>(reader);
                }

                if (operation is ContentOperations.Add)
                {
                   if (!logins.AddEmailToExceptionsList(emailAddress))
                    {
                        return;
                    };
                   
                }
                
                if (operation is ContentOperations.Remove)
                {
                    if(!logins.RemoveEmailFromExceptionsList(emailAddress))
                    {
                        return;
                    };
                }

                var result = new LoginDiscontinuation(
                   logins.DiscontinuedForEmailAddresses,
                   logins.DiscontinuedForDomains,
                   logins.ExceptionsForEmailAddresses,
                   logins.ForceTransformationToOrganizationForEmailAddresses,
                   logins.EnabledOrganizationAadTenants,
                   logins.IsPasswordDiscontinuedForAll);

                var saveResult = await TrySaveAsync(result, reference.ContentId);
                if (saveResult == ContentSaveResult.Ok)
                {
                    return;
                }
                
                _logger.LogWarning(
                    "Failed to {operation} emailAddress from exception list, attempt {Attempt} of {MaxAttempts}...",
                    operation.ToString(),
                    attempt + 1,
                    MaxAttempts);
            }

            throw new InvalidOperationException($"Unable to add/remove emailAddress from exception list after {MaxAttempts} attempts");
        }

        public async Task<ContentSaveResult> TrySaveAsync(LoginDiscontinuation loginDiscontinuation, string contentId)
        {
            var result = await TrySaveInternalAsync(loginDiscontinuation, contentId);

            return result;
        }

        public async Task<IReadOnlyList<string>> GetListOfExceptionEmailList()
        {       
            var loginDiscontinuation = await GetAsync();

            return loginDiscontinuation.ExceptionsForEmailAddresses.ToList();
        }

        private async Task<ContentSaveResult> TrySaveInternalAsync(LoginDiscontinuation loginDiscontinuationConfig, string contentId)
        {
            var accessCondition = AccessConditionWrapper.GenerateIfMatchCondition(contentId);

            try
            {
                using (var stream = new MemoryStream())
                using (var writer = new StreamWriter(stream))
                using (var jsonWriter = new JsonTextWriter(writer))
                {
                    _serializer.Serialize(jsonWriter, loginDiscontinuationConfig);
                    jsonWriter.Flush();
                    stream.Position = 0;

                    await _storage.SaveFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName, stream, accessCondition);

                    return ContentSaveResult.Ok;
                }
            }
            catch (CloudBlobPreconditionFailedException)
            {
                return ContentSaveResult.Conflict;
            }
        }
    }
}