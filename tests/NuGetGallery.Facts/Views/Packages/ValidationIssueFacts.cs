// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using RazorEngine.Configuration;
using RazorEngine.Templating;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.Views.Packages
{
    public class ValidationIssueFacts
    {
        private const string UnknownIssueMessage = "<strong>Package publishing failed.</strong> This package could " +
            "not be published since package validation failed. Please contact <a href=\"mailto:support@nuget.org\">" +
            "support@nuget.org</a>.";
        private readonly ITestOutputHelper _output;

        public ValidationIssueFacts(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(HasACaseForAllIssueTypesTestData))]
        public void HasACaseForAllIssueTypes(ValidationIssue issue)
        {
            // Arrange & Act
            var html = CompileView(issue);

            // Assert
            Assert.DoesNotContain(UnknownIssueMessage, html);
        }

        [Theory]
        [MemberData(nameof(HasExpectedMessageForUnknownIssueTestData))]
        public void HasExpectedMessageForUnknownIssue(ValidationIssue issue)
        {
            // Arrange & Act
            var html = CompileView(issue);

            // Assert
            Assert.Equal(UnknownIssueMessage, html);
        }

        [Theory]
        [MemberData(nameof(AllIssueCodesAreHandledTestData))]
        public void AllIssueCodesAreHandled(ValidationIssueCode issueCode)
        {
            var issueCodes = new HashSet<ValidationIssueCode>(KnownValidationIssues
                .Select(x => x.IssueCode)
                .Concat(UnknownValidationIssues
                    .Select(x => x.IssueCode)));

            Assert.Contains(issueCode, issueCodes);
        }

        private string CompileView(ValidationIssue issue)
        {
            // Arrange
            var config = new TemplateServiceConfiguration
            {
                TemplateManager = new EmbeddedResourceTemplateManager(GetType()),
                DisableTempFileLocking = true,
            };

            using (var razorEngine = RazorEngineService.Create(config))
            {
                _output.WriteLine($"Issue code: {issue.IssueCode}");
                _output.WriteLine($"Serialized: {issue.Serialize()}");

                // Act
                var html = CollapseWhitespace(razorEngine.RunCompile("_ValidationIssue", model: issue))
                    .Trim();

                _output.WriteLine($"HTML:");
                _output.WriteLine(html);

                return html;
            }   
        }

        private string CollapseWhitespace(string input)
        {
            return Regex.Replace(input, @"\s+", " ");
        }

        public static IEnumerable<ValidationIssue> KnownValidationIssues
        {
            get
            {
                yield return new PackageIsSigned();
                yield return new ClientSigningVerificationFailure("NU3000", "Some signing error.");
            }
        }

        public static IEnumerable<ValidationIssue> UnknownValidationIssues
        {
            get
            {
                yield return new UnknownIssue();
#pragma warning disable 0618
                yield return new ObsoleteTestingIssue("a", 1);
#pragma warning restore 0618
            }
        }

        public static IEnumerable<object[]> AllIssueCodesAreHandledTestData => Enum
            .GetValues(typeof(ValidationIssueCode))
            .Cast<ValidationIssueCode>()
            .Select(x => new object[] { x });

        public static IEnumerable<object[]> HasACaseForAllIssueTypesTestData => KnownValidationIssues
            .Select(x => new object[] { x });

        public static IEnumerable<object[]> HasExpectedMessageForUnknownIssueTestData => UnknownValidationIssues
            .Select(x => new object[] { x });
    }
}
