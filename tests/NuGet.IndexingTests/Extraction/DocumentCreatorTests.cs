// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Documents;
using NuGet.Indexing;
using Xunit;

namespace NuGet.IndexingTests
{
    public class DocumentCreatorTests
    {
        [Theory, MemberData(nameof(VerifiesRequiredPropertiesAreProvidedData))]
        public void VerifiesRequiredPropertiesAreProvided(string key, string expected)
        {
            // Arrange
            var package = GetPackage();
            package.Remove(key);

            // Act, Assert
            var exception = Assert.Throws<Exception>(() => DocumentCreator.CreateDocument(package));
            Assert.Equal(expected, exception.Message);
        }

        [Theory, MemberData(nameof(ValidatesPropertiesThatAreNotStringsData))]
        public void ValidatesPropertiesThatAreNotStrings(string key, string expected)
        {
            // Arrange
            var package = GetPackage();
            package[key] = "bad";

            // Act, Assert
            var exception = Assert.Throws<Exception>(() => DocumentCreator.CreateDocument(package));
            Assert.Equal(expected, exception.Message);
        }

        [Fact]
        public void RejectsMissingVersionAndOriginalVersion()
        {
            // Arrange
            var package = GetPackage();
            package.Remove("version");
            package.Remove("originalVersion");

            // Act, Assert
            var exception = Assert.Throws<Exception>(() => DocumentCreator.CreateDocument(package));
            Assert.Equal("Required property 'version' or 'originalVersion' not found.\r\n", exception.Message);
        }

        [Fact]
        public void RejectsInvalidOriginalVersionWhenVersionIsNotProvided()
        {
            // Arrange
            var package = GetPackage();
            package.Remove("version");
            package["originalVersion"] = "bad";

            // Act, Assert
            var exception = Assert.Throws<Exception>(() => DocumentCreator.CreateDocument(package));
            Assert.Equal("Unable to parse 'originalVersion' as NuGetVersion.\r\nRequired property 'version' or 'originalVersion' not found.\r\n", exception.Message);
        }

        [Fact]
        public void AllAnalyzedFieldsHasPositionsAndOffsetsStored()
        {
            // Arrange
            var package = GetPackage();

            // Act
            var document = DocumentCreator.CreateDocument(package);

            // Assert
            foreach (var fieldable in document.GetFields().Where(f => f.IsTokenized && !(f is NumericField)))
            {
                Assert.True(fieldable.IsTermVectorStored, $"{fieldable.Name} should have its term vector stored.");
                Assert.True(fieldable.IsStoreOffsetWithTermVector, $"{fieldable.Name} should store offsets with its term vector.");
                Assert.True(fieldable.IsStorePositionWithTermVector, $"{fieldable.Name} should store positions with its term vector.");
            }
        }

        [Fact]
        public void AllFieldsAreStoredAndIndexed()
        {
            // Arrange
            var package = GetPackage();

            // Act
            var document = DocumentCreator.CreateDocument(package);

            // Assert
            foreach (var field in document.GetFields())
            {
                Assert.True(field.IsStored, $"{field.Name} should be stored.");
                Assert.True(field.IsIndexed, $"{field.Name} should be indexed.");
            }
        }

        [Fact]
        public void DefaultsMissingTitleToValueOfId()
        {
            // Arrange
            var package = GetPackage();
            package.Remove("title");

            // Act
            var document = DocumentCreator.CreateDocument(package);

            // Assert
            Assert.Equal("DotNetZip", document.GetFieldable("Title").StringValue);
            Assert.Equal("dotnetzip", document.GetFieldable("SortableTitle").StringValue);
        }

        [Fact]
        public void DefaultsMissingLastEditedToValueOfPublished()
        {
            // Arrange
            var package = GetPackage();
            package.Remove("lastEdited");

            // Act
            var document = DocumentCreator.CreateDocument(package);

            // Assert
            Assert.Equal("2002-02-02T00:00:00.0000000Z", document.GetField("OriginalPublished").StringValue);
            Assert.Null(document.GetField("OriginalLastEdited"));
            Assert.Equal("20020202", document.GetFieldable("PublishedDate").StringValue);
            Assert.Equal("20020202", document.GetFieldable("LastEditedDate").StringValue);
        }

        [Fact]
        public void DefaultsMissingVersionToParsedOriginalVersion()
        {
            // Arrange
            var package = GetPackage();
            package.Remove("version");
            package["originalVersion"] = "1.02.003";

            // Act
            var document = DocumentCreator.CreateDocument(package);

            // Assert
            Assert.Equal("1.02.003", document.GetFieldable("OriginalVersion").StringValue);
            Assert.Equal("1.2.3", document.GetFieldable("Version").StringValue);
        }

        [Fact]
        public void HasExpectedFieldNamesAndValues()
        {
            // Arrange
            var package = GetPackage();
            var expected = new[]
            {
                new KeyValuePair<string, string>("Id", "DotNetZip"),
                new KeyValuePair<string, string>("IdAutocomplete", "DotNetZip"),
                new KeyValuePair<string, string>("TokenizedId", "DotNetZip"),
                new KeyValuePair<string, string>("ShingledId", "DotNetZip"),
                new KeyValuePair<string, string>("OriginalVersion", "1.00.000"),
                new KeyValuePair<string, string>("Version", "1.0.0"),
                new KeyValuePair<string, string>("Title", "The Famous DotNetZip"),
                new KeyValuePair<string, string>("Description", "The description."),
                new KeyValuePair<string, string>("Summary", "The summary."),
                new KeyValuePair<string, string>("Tags", "dot net zip"),
                new KeyValuePair<string, string>("Authors", "Justin Bieber, Nick Jonas"),
                new KeyValuePair<string, string>("Listed", "true"),
                new KeyValuePair<string, string>("OriginalCreated", "2001-01-01T00:00:00.0000000Z"),
                new KeyValuePair<string, string>("OriginalPublished", "2002-02-02T00:00:00.0000000Z"),
                new KeyValuePair<string, string>("PublishedDate", "20020202"),
                new KeyValuePair<string, string>("OriginalLastEdited", "2003-03-03T00:00:00.0000000Z"),
                new KeyValuePair<string, string>("LastEditedDate", "20030303"),
                new KeyValuePair<string, string>("SortableTitle", "the famous dotnetzip"),
                new KeyValuePair<string, string>("IconUrl", "http://example/icon.png"),
                new KeyValuePair<string, string>("ProjectUrl", "http://example/"),
                new KeyValuePair<string, string>("MinClientVersion", "2.0.0"),
                new KeyValuePair<string, string>("ReleaseNotes", "The release notes."),
                new KeyValuePair<string, string>("Copyright", "The copyright."),
                new KeyValuePair<string, string>("Language", "English"),
                new KeyValuePair<string, string>("LicenseUrl", "http://example/license.txt"),
                new KeyValuePair<string, string>("PackageHash", "0123456789ABCDEF"),
                new KeyValuePair<string, string>("PackageHashAlgorithm", "SHA1"),
                new KeyValuePair<string, string>("PackageSize", "1200"),
                new KeyValuePair<string, string>("RequiresLicenseAcceptance", "true"),
                new KeyValuePair<string, string>("FlattenedDependencies", "Lucene.Net:2.9.4.1|WindowsAzure.Storage:1.6"),
                new KeyValuePair<string, string>("Dependencies", "[{\"Id\":\"Lucene.Net\",\"VersionSpec\":\"2.9.4.1\"},{\"Id\":\"WindowsAzure.Storage\",\"VersionSpec\":\"1.6\"}]"),
                new KeyValuePair<string, string>("SupportedFrameworks", "[\"net40\",\"aspnet99\"]")
            };

            // Act
            var document = DocumentCreator.CreateDocument(package);

            // Assert
            var actual = document.GetFields().Select(f => new KeyValuePair<string, string>(f.Name, f.StringValue)).ToArray();
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }

        public static IEnumerable<string[]> VerifiesRequiredPropertiesAreProvidedData
        {
            get
            {
                yield return new[] { "id", "Required property 'id' not found.\r\n" };
                yield return new[] { "listed", "Required property 'listed' not found.\r\n" };
                yield return new[] { "published", "Required property 'published' not found.\r\n" };
            }
        }

        public static IEnumerable<string[]> ValidatesPropertiesThatAreNotStringsData
        {
            get
            {
                yield return new[] { "listed", "Unable to parse 'listed' as Boolean.\r\n" };
                yield return new[] { "published", "Unable to parse 'published' as DateTime.\r\n" };
                yield return new[] { "lastEdited", "Unable to parse 'lastEdited' as DateTime.\r\n" };
                yield return new[] { "requireLicenseAcceptance", "Unable to parse 'requiresLicenseAcceptance' as Boolean.\r\n" };
                yield return new[] { "packageSize", "Unable to parse 'packageSize' as Int32.\r\n" };
            }
        }

        private static IDictionary<string, string> GetPackage()
        {
            return new Dictionary<string, string>
            {
                // required
                { "id", "DotNetZip" },
                { "version", "1.0.0" },
                { "listed", "true" },
                { "published", new DateTime(2002, 2, 2, 0, 0, 0, DateTimeKind.Utc).ToString("O") },

                // not required but validated
                { "lastEdited", new DateTime(2003, 3, 3, 0, 0, 0, DateTimeKind.Utc).ToString("O") },
                { "packageSize", "1200" },
                { "requireLicenseAcceptance", "true" },

                // not required
                { "originalVersion", "1.00.000" },
                { "title", "The Famous DotNetZip" },
                { "description", "The description." },
                { "summary", "The summary." },
                { "tags", "dot net zip" },
                { "authors", "Justin Bieber, Nick Jonas" },
                { "created", new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O") },
                { "iconUrl", "http://example/icon.png" },
                { "projectUrl", "http://example/" },
                { "minClientVersion", "2.0.0" },
                { "releaseNotes", "The release notes." },
                { "copyright", "The copyright." },
                { "language", "English" },
                { "licenseUrl", "http://example/license.txt" },
                { "packageHash", "0123456789ABCDEF" },
                { "packageHashAlgorithm", "SHA1" },
                { "flattenedDependencies", "Lucene.Net:2.9.4.1|WindowsAzure.Storage:1.6" },
                { "supportedFrameworks", "net40|aspnet99" }
            };
        }
    }
}
