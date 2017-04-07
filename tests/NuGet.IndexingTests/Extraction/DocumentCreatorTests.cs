// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Documents;
using NuGet.Indexing;
using Xunit;
using LuceneConstants = NuGet.Indexing.MetadataConstants.LuceneMetadata;

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
            package.Remove(MetadataConstants.NormalizedVersionPropertyName);
            package.Remove(MetadataConstants.VersionPropertyName);

            // Act, Assert
            var exception = Assert.Throws<Exception>(() => DocumentCreator.CreateDocument(package));
            Assert.Equal($"Required property '{MetadataConstants.NormalizedVersionPropertyName}' or '{MetadataConstants.VersionPropertyName}' not found.\r\n", exception.Message);
        }

        [Fact]
        public void RejectsInvalidOriginalVersionWhenVersionIsNotProvided()
        {
            // Arrange
            var package = GetPackage();
            package.Remove(MetadataConstants.NormalizedVersionPropertyName);
            package[MetadataConstants.VersionPropertyName] = "bad";

            // Act, Assert
            var exception = Assert.Throws<Exception>(() => DocumentCreator.CreateDocument(package));
            Assert.Equal($"Unable to parse '{MetadataConstants.VersionPropertyName}' as NuGetVersion.\r\nRequired property '{MetadataConstants.NormalizedVersionPropertyName}' or '{MetadataConstants.VersionPropertyName}' not found.\r\n", exception.Message);
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
            package.Remove(MetadataConstants.TitlePropertyName);

            // Act
            var document = DocumentCreator.CreateDocument(package);

            // Assert
            Assert.Equal("DotNetZip", document.GetFieldable(LuceneConstants.TitlePropertyName).StringValue);
            Assert.Equal("dotnetzip", document.GetFieldable(LuceneConstants.SortableTitlePropertyName).StringValue);
        }

        [Fact]
        public void DefaultsEmptyTitleToValueOfId()
        {
            // Arrange
            var package = GetPackage();
            package[MetadataConstants.TitlePropertyName] = string.Empty;

            // Act
            var document = DocumentCreator.CreateDocument(package);

            // Assert
            Assert.Equal("DotNetZip", document.GetFieldable(LuceneConstants.TitlePropertyName).StringValue);
            Assert.Equal("dotnetzip", document.GetFieldable(LuceneConstants.SortableTitlePropertyName).StringValue);
        }

        [Fact]
        public void DefaultsMissingLastEditedToValueOfPublished()
        {
            // Arrange
            var package = GetPackage();
            package.Remove(MetadataConstants.LastEditedPropertyName);

            // Act
            var document = DocumentCreator.CreateDocument(package);

            // Assert
            Assert.Equal("2002-02-02T00:00:00.0000000Z", document.GetField(LuceneConstants.OriginalPublishedPropertyName).StringValue);
            Assert.Null(document.GetField(LuceneConstants.OriginalLastEditedPropertyName));
            Assert.Equal("20020202", document.GetFieldable(LuceneConstants.PublishedDatePropertyName).StringValue);
            Assert.Equal("20020202", document.GetFieldable(LuceneConstants.LastEditedDatePropertyName).StringValue);
        }

        [Fact]
        public void DefaultsMissingVersionToParsedOriginalVersion()
        {
            // Arrange
            var package = GetPackage();
            package.Remove(MetadataConstants.NormalizedVersionPropertyName);
            package[MetadataConstants.VersionPropertyName] = "1.02.003";

            // Act
            var document = DocumentCreator.CreateDocument(package);

            // Assert
            Assert.Equal("1.02.003", document.GetFieldable(LuceneConstants.VersionPropertyName).StringValue);
            Assert.Equal("1.2.3", document.GetFieldable(LuceneConstants.NormalizedVersionPropertyName).StringValue);
        }

        [Fact]
        public void HasExpectedFieldNamesAndValues()
        {
            // Arrange
            var package = GetPackage();
            var expected = new[]
            {
                new KeyValuePair<string, string>(LuceneConstants.IdPropertyName, "DotNetZip"),
                new KeyValuePair<string, string>(LuceneConstants.IdAutocompletePropertyName, "DotNetZip"),
                new KeyValuePair<string, string>(LuceneConstants.TokenizedIdPropertyName, "DotNetZip"),
                new KeyValuePair<string, string>(LuceneConstants.ShingledIdPropertyName, "DotNetZip"),
                new KeyValuePair<string, string>(LuceneConstants.VersionPropertyName, "1.00.000"),
                new KeyValuePair<string, string>(LuceneConstants.NormalizedVersionPropertyName, "1.0.0"),
                new KeyValuePair<string, string>(LuceneConstants.TitlePropertyName, "The Famous DotNetZip"),
                new KeyValuePair<string, string>(LuceneConstants.DescriptionPropertyName, "The description."),
                new KeyValuePair<string, string>(LuceneConstants.SummaryPropertyName, "The summary."),
                new KeyValuePair<string, string>(LuceneConstants.TagsPropertyName, "dot net zip"),
                new KeyValuePair<string, string>(LuceneConstants.AuthorsPropertyName, "Justin Bieber, Nick Jonas"),
                new KeyValuePair<string, string>(LuceneConstants.SemVerLevelPropertyName, ""),
                new KeyValuePair<string, string>(LuceneConstants.ListedPropertyName, "true"),
                new KeyValuePair<string, string>(LuceneConstants.OriginalCreatedPropertyName, "2001-01-01T00:00:00.0000000Z"),
                new KeyValuePair<string, string>(LuceneConstants.OriginalPublishedPropertyName, "2002-02-02T00:00:00.0000000Z"),
                new KeyValuePair<string, string>(LuceneConstants.PublishedDatePropertyName, "20020202"),
                new KeyValuePair<string, string>(LuceneConstants.OriginalLastEditedPropertyName, "2003-03-03T00:00:00.0000000Z"),
                new KeyValuePair<string, string>(LuceneConstants.LastEditedDatePropertyName, "20030303"),
                new KeyValuePair<string, string>(LuceneConstants.SortableTitlePropertyName, "the famous dotnetzip"),
                new KeyValuePair<string, string>(LuceneConstants.IconUrlPropertyName, "http://example/icon.png"),
                new KeyValuePair<string, string>(LuceneConstants.ProjectUrlPropertyName, "http://example/"),
                new KeyValuePair<string, string>(LuceneConstants.MinClientVersionPropertyName, "2.0.0"),
                new KeyValuePair<string, string>(LuceneConstants.ReleaseNotesPropertyName, "The release notes."),
                new KeyValuePair<string, string>(LuceneConstants.CopyrightPropertyName, "The copyright."),
                new KeyValuePair<string, string>(LuceneConstants.LanguagePropertyName, "English"),
                new KeyValuePair<string, string>(LuceneConstants.LicenseUrlPropertyName, "http://example/license.txt"),
                new KeyValuePair<string, string>(LuceneConstants.PackageHashPropertyName, "0123456789ABCDEF"),
                new KeyValuePair<string, string>(LuceneConstants.PackageHashAlgorithmPropertyName, "SHA1"),
                new KeyValuePair<string, string>(LuceneConstants.PackageSizePropertyName, "1200"),
                new KeyValuePair<string, string>(LuceneConstants.RequiresLicenseAcceptancePropertyName, "true"),
                new KeyValuePair<string, string>(LuceneConstants.FlattenedDependenciesPropertyName, "Lucene.Net:2.9.4.1|WindowsAzure.Storage:1.6"),
                new KeyValuePair<string, string>(LuceneConstants.DependenciesPropertyName, "[{\"Id\":\"Lucene.Net\",\"VersionSpec\":\"2.9.4.1\"},{\"Id\":\"WindowsAzure.Storage\",\"VersionSpec\":\"1.6\"}]"),
                new KeyValuePair<string, string>(LuceneConstants.SupportedFrameworksPropertyName, "[\"net40\",\"aspnet99\"]")
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
                yield return new[] { MetadataConstants.IdPropertyName, "Required property 'id' not found.\r\n" };
                yield return new[] { MetadataConstants.ListedPropertyName, "Required property 'listed' not found.\r\n" };
                yield return new[] { MetadataConstants.PublishedPropertyName, "Required property 'published' not found.\r\n" };
            }
        }

        public static IEnumerable<string[]> ValidatesPropertiesThatAreNotStringsData
        {
            get
            {
                yield return new[] { MetadataConstants.ListedPropertyName, "Unable to parse 'listed' as Boolean.\r\n" };
                yield return new[] { MetadataConstants.PublishedPropertyName, "Unable to parse 'published' as DateTime.\r\n" };
                yield return new[] { MetadataConstants.LastEditedPropertyName, "Unable to parse 'lastEdited' as DateTime.\r\n" };
                yield return new[] { MetadataConstants.RequiresLicenseAcceptancePropertyName, "Unable to parse 'requiresLicenseAcceptance' as Boolean.\r\n" };
                yield return new[] { MetadataConstants.PackageSizePropertyName, "Unable to parse 'packageSize' as Int32.\r\n" };
            }
        }

        private static IDictionary<string, string> GetPackage()
        {
            return new Dictionary<string, string>
            {
                // required
                { MetadataConstants.IdPropertyName, "DotNetZip" },
                { MetadataConstants.NormalizedVersionPropertyName, "1.0.0" },
                { MetadataConstants.ListedPropertyName, "true" },
                { MetadataConstants.PublishedPropertyName, new DateTime(2002, 2, 2, 0, 0, 0, DateTimeKind.Utc).ToString("O") },

                // not required but validated
                { MetadataConstants.LastEditedPropertyName, new DateTime(2003, 3, 3, 0, 0, 0, DateTimeKind.Utc).ToString("O") },
                { MetadataConstants.PackageSizePropertyName, "1200" },
                { MetadataConstants.RequiresLicenseAcceptancePropertyName, "true" },

                // not required
                { MetadataConstants.SemVerLevelKeyPropertyName, "" },
                { MetadataConstants.VersionPropertyName, "1.00.000" },
                { MetadataConstants.TitlePropertyName, "The Famous DotNetZip" },
                { MetadataConstants.DescriptionPropertyName, "The description." },
                { MetadataConstants.SummaryPropertyName, "The summary." },
                { MetadataConstants.TagsPropertyName, "dot net zip" },
                { MetadataConstants.AuthorsPropertyName, "Justin Bieber, Nick Jonas" },
                { MetadataConstants.CreatedPropertyName, new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O") },
                { MetadataConstants.IconUrlPropertyName, "http://example/icon.png" },
                { MetadataConstants.ProjectUrlPropertyName, "http://example/" },
                { MetadataConstants.MinClientVersionPropertyName, "2.0.0" },
                { MetadataConstants.ReleaseNotesPropertyName, "The release notes." },
                { MetadataConstants.CopyrightPropertyName, "The copyright." },
                { MetadataConstants.LanguagePropertyName, "English" },
                { MetadataConstants.LicenseUrlPropertyName, "http://example/license.txt" },
                { MetadataConstants.PackageHashPropertyName, "0123456789ABCDEF" },
                { MetadataConstants.PackageHashAlgorithmPropertyName, "SHA1" },
                { MetadataConstants.FlattenedDependenciesPropertyName, "Lucene.Net:2.9.4.1|WindowsAzure.Storage:1.6" },
                { MetadataConstants.SupportedFrameworksPropertyName, "net40|aspnet99" }
            };
        }
    }
}
