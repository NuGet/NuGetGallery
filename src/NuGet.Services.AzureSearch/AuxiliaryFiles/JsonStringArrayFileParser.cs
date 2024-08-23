﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public static class JsonStringArrayFileParser
    {
        /// <summary>
        /// Load the auxiliary data in simple json string array format.
        /// </summary>
        /// <param name="fileName">The name of the file that contains the auxiliary data</param>
        /// <param name="loader">The loader that should be used to fetch the file's content</param>
        /// <param name="logger">The logger</param>
        /// <returns>A case-insensitive set of all the strings in the json array</returns>
        public static HashSet<string> Load(JsonReader reader, ILogger logger)
        {
            try
            {
                return Parse(reader);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load JSON string array.");

                throw;
            }
        }

        /// <summary>
        /// Parse the string from the input.
        /// </summary>
        /// <param name="reader">The reader whose content should be parsed</param>
        /// <returns>A case-insensitive set of all the verified packages</returns>
        public static HashSet<string> Parse(JsonReader reader)
        {
            // The file should contain an array of strings, such as: ["Package1","Package2", ...]
            reader.Read();
            ThrowIfNotExpectedToken(reader, JsonToken.StartArray);

            // Read all of the strings from the JSON array.
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stringValue = reader.ReadAsString();

            while (stringValue != null)
            {
                // Package IDs strings are likely to be duplicates from previous reloads. We'll reuse the
                // interned strings so that duplicated strings can be garbage collected right away.
                result.Add(String.Intern(stringValue));

                stringValue = reader.ReadAsString();
            }

            ThrowIfNotExpectedToken(reader, JsonToken.EndArray);

            return result;
        }

        private static void ThrowIfNotExpectedToken(JsonReader reader, JsonToken expected)
        {
            if (reader.TokenType != expected)
            {
                throw new InvalidDataException($"Malformed simple json string array auxiliary file - expected '{JsonToken.StartArray}', actual: '{reader.TokenType}'");
            }
        }
    }
}
