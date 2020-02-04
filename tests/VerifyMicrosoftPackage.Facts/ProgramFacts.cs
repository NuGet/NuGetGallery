// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGetGallery.Security;
using NuGetGallery.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.VerifyMicrosoftPackage.Facts
{
    public class ProgramFacts : IDisposable
    {
        private readonly TextOutputWriter _console;
        private readonly TestDirectory _directory;

        public ProgramFacts(ITestOutputHelper output)
        {
            _console = new TextOutputWriter(output);
            _directory = TestDirectory.Create();
        }

        [Fact]
        public void ReturnsNegativeOneForNoArguments()
        {
            var args = new string[0];

            var exitCode = Program.Run(args, _console);

            Assert.Equal(-1, exitCode);
        }

        [Theory]
        [InlineData("-?")]
        [InlineData("-h")]
        [InlineData("--help")]
        public void ReturnsNegativeOneForHelp(string argument)
        {
            var args = new[] { argument };

            var exitCode = Program.Run(args, _console);

            Assert.Equal(-1, exitCode);
        }

        [Fact]
        public void ReturnsNegativeTwoForUnexpectedOption()
        {
            var args = new[] { "--fake" };

            var exitCode = Program.Run(args, _console);

            Assert.Equal(-1, exitCode);
            Assert.Contains("Unrecognized option '--fake'", _console.Messages);
        }

        [Fact]
        public void ReturnsNegativeTwoForException()
        {
            File.WriteAllBytes(Path.Combine(_directory, "bad.nupkg"), new byte[0]);
            var args = new[] { Path.Combine(_directory, "*.nupkg") };

            var exitCode = Program.Run(args, _console);

            Assert.Equal(-2, exitCode);
            Assert.Contains("An exception occurred.", _console.Messages);
        }

        [Fact]
        public void ReturnsZeroForNoPackages()
        {
            var args = new[] { Path.Combine(_directory, "*.nupkg") };

            var exitCode = Program.Run(args, _console);

            Assert.Equal(0, exitCode);
            AssertCounts(valid: 0, invalid: 0);
        }

        [Fact]
        public void RecursiveFindsPackagesInChildDirectories()
        {
            var args = new[] { Path.Combine(_directory, "*.nupkg"), "--recursive" };
            CreatePackage(Path.Combine("inner", "testA.nupkg"));
            CreatePackage(Path.Combine("inner", "testB.nupkg"));

            var exitCode = Program.Run(args, _console);

            Assert.Equal(0, exitCode);
            AssertCounts(valid: 2, invalid: 0);
        }

        [Fact]
        public void RecursiveContinuesAfterFailures()
        {
            var args = new[] { Path.Combine(_directory, "*.nupkg"), "--recursive" };
            CreatePackage(Path.Combine("inner", "testA.nupkg"));
            CreatePackage(Path.Combine("inner", "testB.nupkg"), authors: "Not Microsoft");

            var exitCode = Program.Run(args, _console);

            Assert.Equal(1, exitCode);
            AssertCounts(valid: 1, invalid: 1);
        }

        [Fact]
        public void ChecksMultiplePackages()
        {
            var args = new[]
            {
                Path.Combine(_directory, "inner", "testA.nupkg"),
                Path.Combine(_directory, "inner", "testB.nupkg"),
            };
            CreatePackage(Path.Combine("inner", "testA.nupkg"));
            CreatePackage(Path.Combine("inner", "testB.nupkg"));

            var exitCode = Program.Run(args, _console);

            Assert.Equal(0, exitCode);
            AssertCounts(valid: 2, invalid: 0);
        }

        [Fact]
        public void ValidatesASingleInvalidPackage()
        {
            var args = new[]
            {
                Path.Combine(_directory, "inner", "testA.nupkg"),
            };
            CreatePackage(Path.Combine("inner", "testA.nupkg"), authors: "Not Microsoft");

            var exitCode = Program.Run(args, _console);

            Assert.Equal(1, exitCode);
            AssertCounts(valid: 0, invalid: 1);
        }

        [Fact]
        public void ValidatesASingleValidPackage()
        {
            var args = new[]
            {
                Path.Combine(_directory, "inner", "testA.nupkg"),
            };
            CreatePackage(Path.Combine("inner", "testA.nupkg"));

            var exitCode = Program.Run(args, _console);

            Assert.Equal(0, exitCode);
            AssertCounts(valid: 1, invalid: 0);
        }

        [Fact]
        public void OutputsDefaultRuleSet()
        {
            var ruleSetPath = Path.Combine(_directory, "child", "a", "b", "rules.json");
            var args = new[]
            {
                "--rule-set",
                ruleSetPath,
                "--write-default-rule-set"
            };

            var exitCode = Program.Run(args, _console);

            Assert.Equal(0, exitCode);
            Assert.DoesNotContain("A rule set already exists at this path. It will be replaced.", _console.Messages);
            Assert.True(File.Exists(ruleSetPath), $"The rule set should now exist at this path: {ruleSetPath}");
        }

        [Fact]
        public void ReplacesExistingRuleSetWithDefault()
        {
            var ruleSetPath = Path.Combine(_directory, "rules.json");
            File.WriteAllText(ruleSetPath, "{}");
            var args = new[]
            {
                "--rule-set",
                ruleSetPath,
                "--write-default-rule-set"
            };

            var exitCode = Program.Run(args, _console);

            Assert.Equal(0, exitCode);
            Assert.Contains("A rule set already exists at this path. It will be replaced.", _console.Messages);
            Assert.True(File.Exists(ruleSetPath), $"The rule set should now exist at this path: {ruleSetPath}");
        }

        [Fact]
        public void UsesCustomRuleSetForValidPackage()
        {
            var state = new RequirePackageMetadataState
            {
                AllowedCopyrightNotices = new[] { "My copyright" },
                AllowedAuthors = new[] { "My authors" },
                IsLicenseUrlRequired = false,
                IsProjectUrlRequired = false,
            };
            var ruleSetPath = Path.Combine(_directory, "rules.json");
            File.WriteAllText(ruleSetPath, JsonConvert.SerializeObject(state));
            var args = new[]
            {
                Path.Combine(_directory, "inner", "testA.nupkg"),
                "--rule-set",
                ruleSetPath
            };

            CreatePackage(
                Path.Combine("inner", "testA.nupkg"),
                copyright: state.AllowedCopyrightNotices[0],
                authors: state.AllowedAuthors[0],
                licenseUrl: null,
                projectUrl: null);

            var exitCode = Program.Run(args, _console);

            Assert.Equal(0, exitCode);
            AssertCounts(valid: 1, invalid: 0);
        }

        [Fact]
        public void UsesCustomRuleSetForInvalidPackage()
        {
            var state = new RequirePackageMetadataState
            {
                AllowedCopyrightNotices = new[] { "My copyright" },
                AllowedAuthors = new[] { "My authors" },
            };
            var ruleSetPath = Path.Combine(_directory, "rules.json");
            File.WriteAllText(ruleSetPath, JsonConvert.SerializeObject(state));
            var args = new[]
            {
                Path.Combine(_directory, "inner", "testA.nupkg"),
                "--rule-set",
                ruleSetPath
            };

            CreatePackage(Path.Combine("inner", "testA.nupkg"));

            var exitCode = Program.Run(args, _console);

            Assert.Equal(1, exitCode);
            AssertCounts(valid: 0, invalid: 1);
            Assert.Contains("  - The package metadata defines 'Microsoft' as one of the authors which is not allowed by policy.", _console.Messages);
            Assert.Contains("  - The package metadata contains a non-compliant copyright element.", _console.Messages);
        }

        [Fact]
        public void ContinuesAfterFailure()
        {
            var args = new[]
            {
                Path.Combine(_directory, "inner", "testA.nupkg"),
                Path.Combine(_directory, "inner", "testB.nupkg"),
            };
            CreatePackage(Path.Combine("inner", "testA.nupkg"), authors:"Not Microsoft");
            CreatePackage(Path.Combine("inner", "testB.nupkg"));

            var exitCode = Program.Run(args, _console);

            Assert.Equal(1, exitCode);
            AssertCounts(valid: 1, invalid: 1);
        }

        [Fact]
        public void ExitCodeIsNumberOfFailures()
        {
            var args = new[]
            {
                Path.Combine(_directory, "inner", "testA.nupkg"),
                Path.Combine(_directory, "inner", "testB.nupkg"),
                Path.Combine(_directory, "inner", "testC.nupkg"),
                Path.Combine(_directory, "inner", "testD.nupkg"),
            };
            CreatePackage(Path.Combine("inner", "testA.nupkg"), authors: "Not Microsoft");
            CreatePackage(Path.Combine("inner", "testB.nupkg"));
            CreatePackage(Path.Combine("inner", "testC.nupkg"), authors: "Not Microsoft");
            CreatePackage(Path.Combine("inner", "testD.nupkg"), authors: "Not Microsoft");

            var exitCode = Program.Run(args, _console);

            Assert.Equal(3, exitCode);
            AssertCounts(valid: 1, invalid: 3);
        }

        private void AssertCounts(int valid, int invalid)
        {
            Assert.Contains($"Valid package count: {valid}", _console.Messages);
            Assert.Contains($"Invalid package count: {invalid}", _console.Messages);
        }

        public void Dispose()
        {
            _directory.Dispose();
        }

        private void CreatePackage(
            string relativePath,
            string authors = "Microsoft",
            string copyright = "© Microsoft Corporation. All rights reserved.",
            string licenseUrl = "https://aka.ms/nugetlicense",
            string projectUrl = "https://aka.ms/nugetprj")
        {
            var fullPath = Path.Combine(_directory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(fullPath)));

            var packageBuilder = new PackageBuilder();

            packageBuilder.Id = $"TestPackage-{Guid.NewGuid()}";
            packageBuilder.Version = NuGetVersion.Parse("1.0.0");
            if (authors != null)
            {
                packageBuilder.Authors.Add(authors);
            }
            packageBuilder.Copyright = copyright;
            packageBuilder.Description = "Test package.";
            packageBuilder.LicenseUrl = licenseUrl != null ? new Uri(licenseUrl) : null;
            packageBuilder.ProjectUrl = projectUrl != null ? new Uri(projectUrl) : null;
            packageBuilder.DependencyGroups.Add(new PackageDependencyGroup(
                NuGetFramework.Parse("netstandard1.0"),
                new[]
                {
                    new PackageDependency("Newtonsoft.Json", VersionRange.Parse("9.0.1")),
                }));

            using (var fileStream = File.OpenWrite(fullPath))
            {
                packageBuilder.Save(fileStream);
            }   
        }

        private class TextOutputWriter : TextWriter
        {
            private readonly ITestOutputHelper _output;

            public TextOutputWriter(ITestOutputHelper output)
            {
                _output = output;
            }

            public override Encoding Encoding => Encoding.Default;

            public ConcurrentQueue<string> Messages { get; } = new ConcurrentQueue<string>();

            public override void Write(char value)
            {
                throw new NotImplementedException();
            }

            public override void WriteLine()
            {
                WriteLine(string.Empty);
            }

            public override void WriteLine(string message)
            {
                Messages.Enqueue(message);
                _output.WriteLine(message);
            }
        }
    }
}
