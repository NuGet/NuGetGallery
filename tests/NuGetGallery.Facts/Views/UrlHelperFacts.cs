// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.Views
{
    public class UrlHelperFacts
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public UrlHelperFacts(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Theory]
        [InlineData("Url.Action")]
        [InlineData("Url.Route")]
        public void ViewsDoNotUseDefaultUrlHelperRoutes(string unsupportedTerm)
        {
            // We should not use Url.Action or Url.Route in our views as the gallery
            // may be deployed behind a proxy (such as APIM),
            // in which case the resolved host name would not match the
            // DNS record pointing to the proxy.
            // 
            // To assert that, we look for textual occurrences of Url.Action or Url.Route
            // in all razor files of the NuGetGallery web project.
            AssertNoViolationsOfUnsupportedTermInRazorFiles(unsupportedTerm);
        }

        private void AssertNoViolationsOfUnsupportedTermInRazorFiles(string unsupportedTerm)
        {
            var violations = new ConcurrentBag<Tuple<int, string, string>>();
            var razorFiles = GetRazorFiles();

            // Catch working directory issues.
            Assert.NotEmpty(razorFiles);

            Parallel.ForEach(
                razorFiles,
                file =>
                {
                    var lineNumber = 0;
                    foreach (var line in File.ReadLines(file))
                    {
                        lineNumber++;

                        if (line.Contains(unsupportedTerm))
                        {
                            violations.Add(Tuple.Create(lineNumber, file, line.TrimStart(' ').TrimEnd(' ')));
                        }
                    }
                });

            if (violations.Any())
            {
                _testOutputHelper.WriteLine(
                    $"Avoid usage of '{unsupportedTerm}' in .cshtml files! Consider using a method from 'UrlExtensions.cs' to ensure usage of configured 'SiteRoot' setting.");

                // Pretty-print any violations: group by file
                foreach (var violationsInFile in violations.GroupBy(t => t.Item2).OrderBy(g => g.Key))
                {
                    _testOutputHelper.WriteLine($"Violation(s) in file '{violationsInFile.Key}':");

                    // Order by line number
                    foreach (var violation in violationsInFile.OrderBy(v => v.Item1))
                    {
                        _testOutputHelper.WriteLine(
                            $"  Line #{violation.Item1}: \"{violation.Item3}\"");
                    }
                }

                // Fail the test
                Assert.Empty(violations);
            }
        }

        private static IReadOnlyCollection<string> GetRazorFiles()
        {
            // $(SolutionDir)\tests\NuGetGallery.Facts\bin\Debug
            var pathToViewsFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                @"..\..\..\..\src\NuGetGallery\Views");

            return Directory.GetFiles(
                pathToViewsFolder,
                "*.cshtml",
                SearchOption.AllDirectories)
                .Select(f => Path.GetFullPath(f))
                .ToList();
        }
    }
}
