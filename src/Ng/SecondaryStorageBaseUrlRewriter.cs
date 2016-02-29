// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ng.Json;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace Ng
{
    public class SecondaryStorageBaseUrlRewriter
    {
        private readonly List<KeyValuePair<string, string>> _replacements;

        public SecondaryStorageBaseUrlRewriter(List<KeyValuePair<string, string>> replacements)
        {
            _replacements = replacements;
        }

        public StorageContent Rewrite(
            Uri primaryStorageBaseUri,
            Uri primaryResourceUri,
            Uri secondaryStorageBaseUri,
            Uri secondaryResourceUri,
            StorageContent content)
        {
            JTokenReader tokenReader = null;

            var storageContent = content as JTokenStorageContent;
            if (storageContent != null)
            {
                // Production code should always have JTokenStorageContent
                tokenReader = storageContent.Content.CreateReader() as JTokenReader;
            }
            else
            {
                // Test code may end up here - we need to make sure we have a JTokenReader at our disposal
                using (var streamReader = new StreamReader(content.GetContentStream()))
                {
                    using (var jsonTextReader = new JsonTextReader(streamReader))
                    {
                        tokenReader = JToken.Load(jsonTextReader).CreateReader() as JTokenReader;
                    }
                }
            }

            if (tokenReader != null)
            {
                // Create a rewriting reader
                var rewritingReader = new RegistrationBaseUrlRewritingJsonReader(tokenReader,
                    _replacements.Union(new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>(primaryStorageBaseUri.ToString(), secondaryStorageBaseUri.ToString()),
                        new KeyValuePair<string, string>(GetParentUriString(primaryResourceUri), GetParentUriString(secondaryResourceUri))
                    }).ToList());

                // Clone the original token (passing through our intercepting reader)
                var updatedJson = JToken.Load(rewritingReader);

                // Create new content
                return new JTokenStorageContent(updatedJson, content.ContentType, content.CacheControl);
            }

            return content;
        }
        
        private static string GetParentUriString(Uri uri)
        {
            return uri.AbsoluteUri.Remove(uri.AbsoluteUri.Length - uri.Segments.Last().Length);
        }
    }
}