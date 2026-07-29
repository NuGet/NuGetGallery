// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Reflection;
using Xunit;

namespace NuGetGallery.Services.Authentication
{
    public class GitLabCriteriaFacts
    {
        public class TheCloneMethod
        {
            [Fact]
            public void ClonesAllFields()
            {
                // Arrange - set every private field to a non-default value
                var original = new GitLabCriteria
                {
                    NamespacePath = "my-namespace",
                    NamespaceId = "123",
                    ProjectPath = "my-project",
                    ProjectId = "456",
                    Ref = "main",
                    Environment = "production",
                    ValidateBy = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                };

                // Act
                var clone = original.Clone();

                // Assert - compare every private field via reflection so this test
                // breaks automatically if a new field is added without updating Clone()
                var fields = typeof(GitLabCriteria).GetFields(
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.NotEmpty(fields);

                foreach (var field in fields)
                {
                    var originalValue = field.GetValue(original);
                    var cloneValue = field.GetValue(clone);
                    Assert.True(
                        Equals(originalValue, cloneValue),
                        $"Field '{field.Name}' was not copied by Clone(). " +
                        $"Expected: {originalValue}, Actual: {cloneValue}");
                }
            }

            [Fact]
            public void CloneIsIndependent()
            {
                var original = new GitLabCriteria
                {
                    NamespacePath = "my-namespace",
                    ProjectPath = "my-project",
                    ValidateBy = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                };

                var clone = original.Clone();
                clone.NamespacePath = "other-namespace";

                Assert.Equal("my-namespace", original.NamespacePath);
            }
        }

        public class TheValidateMethod
        {
            [Fact]
            public void ReturnsNullWhenValid()
            {
                var criteria = new GitLabCriteria
                {
                    NamespacePath = "my-namespace",
                    ProjectPath = "my-project",
                    ValidateBy = DateTimeOffset.UtcNow.AddDays(7),
                };

                Assert.Null(criteria.Validate());
            }

            [Fact]
            public void ReturnsNullWhenPermanentlyEnabled()
            {
                var criteria = new GitLabCriteria
                {
                    NamespacePath = "my-namespace",
                    NamespaceId = "123",
                    ProjectPath = "my-project",
                    ProjectId = "456",
                };

                Assert.Null(criteria.Validate());
            }

            [Fact]
            public void RequiresNamespacePath()
            {
                var criteria = new GitLabCriteria
                {
                    ProjectPath = "my-project",
                    ValidateBy = DateTimeOffset.UtcNow.AddDays(7),
                };

                var error = criteria.Validate();
                Assert.NotNull(error);
                Assert.Contains("namespace path", error);
            }

            [Fact]
            public void RequiresProjectPath()
            {
                var criteria = new GitLabCriteria
                {
                    NamespacePath = "my-namespace",
                    ValidateBy = DateTimeOffset.UtcNow.AddDays(7),
                };

                var error = criteria.Validate();
                Assert.NotNull(error);
                Assert.Contains("project path", error);
            }

            [Fact]
            public void RequiresValidateByWhenNotPermanentlyEnabled()
            {
                var criteria = new GitLabCriteria
                {
                    NamespacePath = "my-namespace",
                    ProjectPath = "my-project",
                };

                var error = criteria.Validate();
                Assert.NotNull(error);
                Assert.Contains("validate-by", error);
            }

            [Fact]
            public void ReturnsAllErrorsAtOnce()
            {
                var criteria = new GitLabCriteria();

                var error = criteria.Validate();
                Assert.NotNull(error);
                Assert.Contains("namespace path", error);
                Assert.Contains("project path", error);
                Assert.Contains("validate-by date", error);
            }
        }

        public class TheIsPermanentlyEnabledProperty
        {
            [Fact]
            public void ReturnsTrueWhenBothIdsAreSet()
            {
                var criteria = new GitLabCriteria { NamespaceId = "123", ProjectId = "456" };
                Assert.True(criteria.IsPermanentlyEnabled);
            }

            [Theory]
            [InlineData(null, "456")]
            [InlineData("123", null)]
            [InlineData(null, null)]
            public void ReturnsFalseWhenEitherIdIsMissing(string? namespaceId, string? projectId)
            {
                var criteria = new GitLabCriteria { NamespaceId = namespaceId, ProjectId = projectId };
                Assert.False(criteria.IsPermanentlyEnabled);
            }
        }

        public class TheInitializeValidateByMethod
        {
            [Fact]
            public void ClearsValidateByWhenPermanentlyEnabled()
            {
                var criteria = new GitLabCriteria
                {
                    NamespaceId = "123",
                    ProjectId = "456",
                    ValidateBy = DateTimeOffset.UtcNow.AddDays(7),
                };

                criteria.InitializeValidateBy();

                Assert.Null(criteria.ValidateBy);
            }

            [Fact]
            public void SetsValidateDateAndClearsIdsWhenNotPermanentlyEnabled()
            {
                var criteria = new GitLabCriteria
                {
                    NamespacePath = "my-namespace",
                    NamespaceId = "123",
                    ProjectPath = "my-project",
                    ProjectId = "456",
                };

                // Demote to temporary by clearing IDs before calling
                criteria.NamespaceId = null;
                criteria.ProjectId = null;
                criteria.InitializeValidateBy();

                Assert.Null(criteria.NamespaceId);
                Assert.Null(criteria.ProjectId);
                Assert.NotNull(criteria.ValidateBy);
                Assert.True(criteria.ValidateBy > DateTimeOffset.UtcNow);
            }

            [Fact]
            public void ValidateDateIsRoundedToNearestHour()
            {
                var criteria = new GitLabCriteria
                {
                    NamespacePath = "my-namespace",
                    ProjectPath = "my-project",
                };

                criteria.InitializeValidateBy();

                Assert.Equal(0, criteria.ValidateBy!.Value.Minute);
                Assert.Equal(0, criteria.ValidateBy!.Value.Second);
            }
        }

        public class TheSetters
        {
            [Theory]
            [InlineData("  my-namespace  ", "my-namespace")]
            [InlineData("", "")]
            [InlineData(null, "")]
            public void NamespacePathTrimsWhitespace(string? input, string expected)
            {
                var criteria = new GitLabCriteria { NamespacePath = input! };
                Assert.Equal(expected, criteria.NamespacePath);
            }

            [Theory]
            [InlineData("  my-project  ", "my-project")]
            [InlineData("", "")]
            [InlineData(null, "")]
            public void ProjectPathTrimsWhitespace(string? input, string expected)
            {
                var criteria = new GitLabCriteria { ProjectPath = input! };
                Assert.Equal(expected, criteria.ProjectPath);
            }

            [Theory]
            [InlineData("  main  ", "main")]
            [InlineData("", null)]
            [InlineData(null, null)]
            [InlineData("   ", null)]
            public void RefTrimsAndNormalizesWhitespace(string? input, string? expected)
            {
                var criteria = new GitLabCriteria { Ref = input };
                Assert.Equal(expected, criteria.Ref);
            }
        }

        public class TheToDatabaseJsonMethod
        {
            [Fact]
            public void RoundTripsAllFields()
            {
                var original = new GitLabCriteria
                {
                    NamespacePath = "my-namespace",
                    NamespaceId = "123",
                    ProjectPath = "my-project",
                    ProjectId = "456",
                    Ref = "main",
                    Environment = "production",
                    ValidateBy = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                };

                var json = original.ToDatabaseJson();
                var restored = GitLabCriteria.FromDatabaseJson(json);

                Assert.Equal(original.NamespacePath, restored.NamespacePath);
                Assert.Equal(original.NamespaceId, restored.NamespaceId);
                Assert.Equal(original.ProjectPath, restored.ProjectPath);
                Assert.Equal(original.ProjectId, restored.ProjectId);
                Assert.Equal(original.Ref, restored.Ref);
                Assert.Equal(original.Environment, restored.Environment);
                Assert.Equal(original.ValidateBy, restored.ValidateBy);
            }

            [Fact]
            public void OmitsNullOptionalFields()
            {
                var criteria = new GitLabCriteria
                {
                    NamespacePath = "my-namespace",
                    ProjectPath = "my-project",
                    ValidateBy = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                };

                var json = criteria.ToDatabaseJson();

                Assert.DoesNotContain("ref", json);
                Assert.DoesNotContain("environment", json);
                Assert.DoesNotContain("namespaceId", json);
                Assert.DoesNotContain("projectId", json);
            }
        }
    }
}
