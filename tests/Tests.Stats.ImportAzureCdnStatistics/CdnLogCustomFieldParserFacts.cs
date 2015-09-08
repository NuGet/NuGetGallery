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
            var customField = "\"NuGet-Operation: Install-Package NuGet-DependentPackage: - NuGet-ProjectGuids: {349c5851-65df-11da-9384-00065b846f21};{fae04ec0-301f-11d3-bf4b-00c04f79efbc} - UA: Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)\"";
            var customFields = CdnLogCustomFieldParser.Parse(customField);

            Assert.True(customFields.ContainsKey("NuGet-Operation"));
            Assert.Equal("Install-Package", customFields["NuGet-Operation"]);

            Assert.True(customFields.ContainsKey("NuGet-DependentPackage"));
            Assert.Equal("-", customFields["NuGet-DependentPackage"]);

            Assert.True(customFields.ContainsKey("NuGet-ProjectGuids"));
            Assert.Equal("{349c5851-65df-11da-9384-00065b846f21};{fae04ec0-301f-11d3-bf4b-00c04f79efbc}", customFields["NuGet-ProjectGuids"]);

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