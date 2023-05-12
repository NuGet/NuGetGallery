// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Services.FeatureFlags;

namespace NuGetGallery.Login
{
    public class LoginDiscontinuationFileStorageService: ILoginDiscontinuationFileStorageService
    {
        protected readonly ICoreFileStorageService _storage;
        protected static readonly JsonSerializer _serializer;

        static LoginDiscontinuationFileStorageService()
        {
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

        public LoginDiscontinuationFileStorageService(ICoreFileStorageService storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public async Task<LoginDiscontinuation> GetAsync()
        {
            using (var stream = await _storage.GetFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName))
            {
                return ReadLoginDiscontinuationFromStream(stream);
            }
        }

        protected LoginDiscontinuation ReadLoginDiscontinuationFromStream(Stream stream)
        {
            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {
                return _serializer.Deserialize<LoginDiscontinuation>(reader);
            }
        }
    }

}
