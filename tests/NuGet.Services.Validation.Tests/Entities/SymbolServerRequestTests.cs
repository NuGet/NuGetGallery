// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.Validation.Tests.Entities
{
    public class SymbolServerRequestTests
    {
        private static readonly IReadOnlyDictionary<int, SymbolsPackageIngestRequestStatus> ExpectedRequestStatus = 
            new Dictionary<int, SymbolsPackageIngestRequestStatus>
        {
            { 0, SymbolsPackageIngestRequestStatus.Ingested },
            { 1, SymbolsPackageIngestRequestStatus.Ingesting },
            { 2, SymbolsPackageIngestRequestStatus.FailedIngestion }
        };

        [Theory]
        [MemberData(nameof(HasExpectedRequestStatusValuesData))]
        public void HasExpectedRequestStatusValues(int expected, SymbolsPackageIngestRequestStatus actual)
        {
            Assert.Equal((SymbolsPackageIngestRequestStatus)expected, actual);
        }

        [Fact]
        public void CanCreateSymbolServerRequest()
        {
            // Arrange
            var request = new SymbolsServerRequest()
            {
                Created = new DateTime(2018, 4, 1),
                RequestName = "vstsrequest",
                RequestStatusKey = SymbolsPackageIngestRequestStatus.Ingested,
                SymbolsKey = 7,
                LastUpdated = new DateTime(2018, 4, 1)
            };

            // Assert 
            Assert.Equal("vstsrequest", request.RequestName);
            Assert.Equal(7, request.SymbolsKey);
            Assert.Equal(2018, request.Created.Year);
            Assert.Equal(2018, request.LastUpdated.Year);
            Assert.Equal(SymbolsPackageIngestRequestStatus.Ingested, request.RequestStatusKey);
        }

        public static IEnumerable<object[]> HasExpectedRequestStatusValuesData => ExpectedRequestStatus
           .Select(x => new object[] { x.Key, x.Value });
    }
}
