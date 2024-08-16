// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Stats.AzureCdnLogs.Common;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class CdnLogEntryParserFacts
    {
        public class TheParseLogEntryFromLineMethod
        {
            private const int LineNumber = 42;
            private const string StatusLineFormat = "1507030253 0 - - 0.1.2.3 443 {0} 577 GET http://example/path - 0 653  \"UserAgent\" 56086 \"NuGet-Operation: - NuGet-DependentPackage: - NuGet-ProjectGuids: -\"  ";
            private const string IncompleteDataLine = "1433257489 27 - 0 127.0.0.1 443 HIT/200 2788 GET http://localhost/packages/packageId/packageVersion/icon - 0 844  userAgent (compatible; ";
            private const string IncorrectFormatDataLine = "1433257489 incorrectFormat - 0 127.0.0.1 443 HIT/200 2788 GET http://localhost/packages/packageId/packageVersion/icon - 0 844  userAgent (compatible; ";

            [Theory]
            [InlineData("TCP_MISS/0")]
            [InlineData("TCP_MISS/199")]
            [InlineData("TCP_MISS/300")]
            [InlineData("TCP_MISS/404")]
            [InlineData("SOMETHING_ELSE/404")]
            [InlineData("TCP_MISS/504")]
            [InlineData("TCP_MISS/604")]
            [InlineData("0")]
            [InlineData("304")]
            [InlineData("400")]
            [InlineData("404")]
            [InlineData("500")]
            public void IgnoresNon200HttpStatusCodes(string status)
            {
                // Arrange
                var line = string.Format(StatusLineFormat, status);

                // Act
                var logEntry = CdnLogEntryParser.ParseLogEntryFromLine(
                    LineNumber,
                    line,
                    FailOnError);

                // Assert
                Assert.Null(logEntry);
            }

            [Theory]
            [InlineData("TCP_MISS/200")]
            [InlineData("TCP_MISS/299")]
            [InlineData("TCP_MISS/")]
            [InlineData("TCP_MISS")]
            [InlineData("200")]
            public void DoesNotIgnore200LevelAndUnrecognizedHttpStatusCodes(string status)
            {
                // Arrange
                var line = string.Format(StatusLineFormat, status);

                // Act
                var logEntry = CdnLogEntryParser.ParseLogEntryFromLine(
                    LineNumber,
                    line,
                    FailOnError);

                // Assert
                Assert.NotNull(logEntry);
                Assert.Equal(status, logEntry.CacheStatusCode);
            }

            [Fact]
            public void IgnoresLinesWithIncorrectFormatDataWithOnErrorCallback()
            {
                // Act
                var logEntry = CdnLogEntryParser.ParseLogEntryFromLine(
                    LineNumber,
                    IncorrectFormatDataLine,
                    FormatExceptionOnError);

                // Assert
                Assert.Null(logEntry);
            }

            [Fact]
            public void ThrowsExceptionForLinesWithIncorrectFormatDataWithoutOnErrorCallback()
            {
                // Act/Assert
                Assert.Throws<FormatException>(() =>
                {
                    CdnLogEntryParser.ParseLogEntryFromLine(
                        LineNumber,
                        IncorrectFormatDataLine,
                        null);
                });
            }

            [Fact]
            public void IgnoresLinesWithIncompleteDataWithOnErrorCallback()
            {
                // Act
                var logEntry = CdnLogEntryParser.ParseLogEntryFromLine(
                    LineNumber,
                    IncompleteDataLine,
                    IndexOutOfRangeExceptionOnError);

                // Assert
                Assert.Null(logEntry);
            }

            [Fact]
            public void ThrowsExceptionForLinesWithIncompleteDataWithoutOnErrorCallback()
            {
                // Act/Assert
                Assert.Throws<IndexOutOfRangeException>(() =>
                {
                    CdnLogEntryParser.ParseLogEntryFromLine(
                        LineNumber,
                        IncompleteDataLine,
                        null);
                });
            }
            
            private static void FormatExceptionOnError(Exception e, int lineNumber)
            {
                Assert.True(e is FormatException);
            }

            private static void IndexOutOfRangeExceptionOnError(Exception e, int lineNumber)
            {
                Assert.True(e is IndexOutOfRangeException);
            }

            private static void FailOnError(Exception e, int lineNumber)
            {
                Assert.Fail("The error action should not be called.");
            }
        }
    }
}