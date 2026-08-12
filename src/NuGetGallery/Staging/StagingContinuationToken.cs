// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace NuGetGallery
{
    /// <summary>
    /// Opaque cursor used by staged-package and group-detail listing. The token embeds the owner and the exact filter
    /// it was issued for so it can never be replayed against a different owner or filter, and it carries only the last
    /// <see cref="StagingEntry"/> key rather than exposing raw database keys in the contract surface. The serialized
    /// payload is authenticated and encrypted by an <see cref="IStagingTokenProtector"/>, so a caller cannot inspect
    /// or forge its owner, filter, or key.
    /// </summary>
    internal sealed class StagingContinuationToken
    {
        public const string AllFilter = "all";
        public const string UngroupedFilter = "ungrouped";
        public const string GroupFilterPrefix = "group:";

        [JsonProperty("o")]
        public int OwnerKey { get; set; }

        [JsonProperty("f")]
        public string Filter { get; set; }

        [JsonProperty("k")]
        public int LastKey { get; set; }

        public static string DescribeFilter(string groupId, bool ungrouped)
        {
            if (groupId != null)
            {
                return GroupFilterPrefix + groupId;
            }

            return ungrouped ? UngroupedFilter : AllFilter;
        }

        public string Encode(IStagingTokenProtector protector)
        {
            var json = JsonConvert.SerializeObject(this);
            return protector.Protect(Encoding.UTF8.GetBytes(json));
        }

        /// <summary>
        /// Decodes and validates a continuation token. Returns null when <paramref name="token"/> is null or empty.
        /// Throws <see cref="StagingApiException"/> with <see cref="StagingApiErrorCodes.InvalidContinuationToken"/>
        /// when the token is malformed, tampered, or was issued for a different owner or filter.
        /// </summary>
        public static StagingContinuationToken Decode(IStagingTokenProtector protector, string token, int ownerKey, string expectedFilter)
        {
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            var payload = protector.Unprotect(token);
            if (payload == null)
            {
                throw Invalid();
            }

            StagingContinuationToken decoded;
            try
            {
                var json = Encoding.UTF8.GetString(payload);
                decoded = JsonConvert.DeserializeObject<StagingContinuationToken>(json);
            }
            catch (Exception ex) when (ex is JsonException || ex is DecoderFallbackException)
            {
                throw Invalid();
            }

            if (decoded == null || decoded.OwnerKey != ownerKey || !string.Equals(decoded.Filter, expectedFilter, StringComparison.Ordinal))
            {
                throw Invalid();
            }

            return decoded;
        }

        private static StagingApiException Invalid()
        {
            return new StagingApiException(HttpStatusCode.BadRequest, StagingApiErrorCodes.InvalidContinuationToken, "The continuation token is invalid for this owner or filter.");
        }
    }
}
