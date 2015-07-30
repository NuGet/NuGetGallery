// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.AzureCdnLogs.Common;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class CdnLogCustomFieldParserFacts
    {
        [Fact]
        public void FromCdnLogCustomFieldProperlyExtractsKeyValuePairs()
        {
            var customField = "\"NuGet-Operation: - UA: Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)\"";
            var customFields = CdnLogCustomFieldParser.Parse(customField);

            Assert.True(customFields.ContainsKey("NuGet-Operation"));
            Assert.Equal("-", customFields["NuGet-Operation"]);

            Assert.True(customFields.ContainsKey("UA"));
            Assert.Equal("Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)", customFields["UA"]);
        }

        [Fact]
        public void FromCdnLogCustomFieldProperlyReturnsEmptyDictionaryForNullValue()
        {
            var customFields = CdnLogCustomFieldParser.Parse(null);

            Assert.NotNull(customFields);
            Assert.Equal(0, customFields.Count);
        }
    }
}