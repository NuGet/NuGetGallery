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
    public class VerifiedPackagesExporterTests
    {
        [Fact]
        public void GetVerifiedPackagesReturnsJsonString()
        {
            var dataReader = new Mock<IDataReader>(MockBehavior.Strict);

            dataReader.SetupGet(x => x.FieldCount)
                .Returns(1);
            dataReader.Setup(x => x.GetName(It.Is<int>(i => i == 0)))
                .Returns("Id");
            dataReader.SetupSequence(x => x.Read())
                .Returns(true)
                .Returns(true)
                .Returns(false);
            dataReader.SetupSequence(x => x.GetString(It.Is<int>(i => i == 0)))
                .Returns("My.Test.Package")
                .Returns("Hello.World");

            var exporter = CreateExporter();
            var actualResult = exporter.GetVerifiedPackages(dataReader.Object);

            Assert.Equal("[\"My.Test.Package\",\"Hello.World\"]", actualResult.ToString(Formatting.None));

            dataReader.VerifyAll();
        }

        private static VerifiedPackagesExporter CreateExporter()
        {
            return new VerifiedPackagesExporter(
                new LoggerFactory().CreateLogger<VerifiedPackagesExporter>(),
                openSqlConnectionAsync: () => null,
                defaultDestinationContainer: new CloudBlobContainer(new Uri("https://nuget.org")),
                defaultVerifiedPackagesScript: "b",
                defaultName: "c",
                commandTimeout: TimeSpan.FromSeconds(10));
        }
    }
}