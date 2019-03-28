// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using Newtonsoft.Json;
using Search.GenerateAuxiliaryData;
using Xunit;

namespace Tests.Search.GenerateAuxiliaryData
{
    public class RankingsExporterTests
    {
        [Fact]
        public void GetRankings_ReturnsRankings()
        {
            var dataReader = new Mock<IDataReader>(MockBehavior.Strict);

            dataReader.SetupGet(x => x.FieldCount)
                .Returns(1);
            dataReader.Setup(x => x.GetName(It.Is<int>(i => i == 0)))
                .Returns("PackageId");
            dataReader.SetupSequence(x => x.Read())
                .Returns(true)
                .Returns(true)
                .Returns(false);
            dataReader.SetupSequence(x => x.GetString(It.Is<int>(i => i == 0)))
                .Returns("a")
                .Returns("b");
            dataReader.Setup(x => x.Close());

            var exporter = CreateExporter();
            var actualResult = exporter.GetRankings(dataReader.Object);

            Assert.Equal("{\"Rank\":[\"a\",\"b\"]}", actualResult.ToString(Formatting.None));

            dataReader.VerifyAll();
        }

        private static RankingsExporter CreateExporter()
        {
            return new RankingsExporter(
                new LoggerFactory().CreateLogger<RankingsExporter>(),
                openSqlConnectionAsync: () => null,
                defaultDestinationContainer: new CloudBlobContainer(new Uri("https://nuget.org")),
                defaultRankingsScript: "b",
                defaultName: "c",
                commandTimeout: TimeSpan.FromSeconds(10));
        }
    }
}