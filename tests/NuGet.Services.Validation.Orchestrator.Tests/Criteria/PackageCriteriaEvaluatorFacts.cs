// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;
using Xunit;

namespace NuGet.Services.Validation
{
    public class PackageCriteriaEvaluatorFacts
    {
        public class TheIsMatchMethod
        {
            private readonly PackageCriteriaEvaluator _target;
            private readonly PackageCriteria _criteria;
            private readonly string _matchingOwner;
            private readonly string _matchingPattern;
            private readonly Package _package;

            public TheIsMatchMethod()
            {
                _target = new PackageCriteriaEvaluator();
                _matchingOwner = "NugetTestAccount";
                _matchingPattern = "E2E.SemVer1Stable.*";
                _criteria = new PackageCriteria
                {
                    ExcludeOwners = new[] { _matchingOwner },
                    IncludeIdPatterns = new[] { _matchingPattern },
                };
                _package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "E2E.SemVer1Stable.170503.225847.7481282",
                        Owners = new List<User>
                        {
                            new User { Username = _matchingOwner },
                        },
                    },
                };
            }

            [Fact]
            public void MatchesByDefault()
            {
                // Arrange
                _criteria.ExcludeOwners = new string[0];
                _criteria.IncludeIdPatterns = new string[0];

                // Act
                var actual = _target.IsMatch(_criteria, _package);

                // Assert
                Assert.True(actual, "By default, match the package.");
            }

            [Fact]
            public void ExcludesMatchingSingleOwner()
            {
                // Arrange
                _criteria.ExcludeOwners = new[] { _matchingOwner };
                _criteria.IncludeIdPatterns = new string[0];

                // Act
                var actual = _target.IsMatch(_criteria, _package);

                // Assert
                Assert.False(actual, "The package should have been excluded due to matching owner username.");
            }
            
            [Fact]
            public void ExcludesMatchingSomeOwners()
            {
                // Arrange
                _criteria.ExcludeOwners = new[] { _matchingOwner, "PersonA", "PersonB" };
                _criteria.IncludeIdPatterns = new string[0];
                _package.PackageRegistration.Owners.Add(new User { Username = "PersonA" });
                _package.PackageRegistration.Owners.Add(new User { Username = "PersonC" });

                // Act
                var actual = _target.IsMatch(_criteria, _package);

                // Assert
                Assert.False(actual, "The package should have been excluded due to matching owner username.");
            }

            [Fact]
            public void MatchingPackageIdOverridesMatchingOwner()
            {
                // Arrange
                _criteria.ExcludeOwners = new[] { _matchingOwner };
                _criteria.IncludeIdPatterns = new[] { _matchingPattern };

                // Act
                var actual = _target.IsMatch(_criteria, _package);

                // Assert
                Assert.True(actual, "The package should have been included due to matching ID pattern.");
            }

            [Fact]
            public void NonMatchingPackageIdPatternDoesNotOverrideMatchingOwner()
            {
                // Arrange
                _criteria.ExcludeOwners = new[] { _matchingOwner };
                _criteria.IncludeIdPatterns = new[] { "DoesNotMatch.*" };

                // Act
                var actual = _target.IsMatch(_criteria, _package);

                // Assert
                Assert.False(actual, "The package should have been excluded due to matching owner username.");
            }

            [Fact]
            public void MatchingPackageIdOverridesDefault()
            {
                // Arrange
                _criteria.ExcludeOwners = new string[0];
                _criteria.IncludeIdPatterns = new[] { _matchingPattern };

                // Act
                var actual = _target.IsMatch(_criteria, _package);

                // Assert
                Assert.True(actual, "The package should have been included due to matching ID pattern.");
            }

            [Fact]
            public void NonMatchingPackageIdWithMatchingOwnerIsNotMatched()
            {
                // Arrange
                _criteria.ExcludeOwners = new[] { _matchingOwner };
                _criteria.IncludeIdPatterns = new[] { "DoesNotMatch.*" };

                // Act
                var actual = _target.IsMatch(_criteria, _package);

                // Assert
                Assert.False(actual, "The package should have been excluded due to matching owner username.");
            }

            [Fact]
            public void MatchingPackageIdWithNonMatchingOwnerIsMatched()
            {
                // Arrange
                _criteria.ExcludeOwners = new[] { "DoesNotMatch" };
                _criteria.IncludeIdPatterns = new[] { _matchingPattern };

                // Act
                var actual = _target.IsMatch(_criteria, _package);

                // Assert
                Assert.True(actual, "The package should have been included due to matching owner username.");
            }

            [Fact]
            public void NonMatchingPackageIdWithNonMatchingOwnerMatches()
            {
                // Arrange
                _criteria.ExcludeOwners = new[] { "DoesNotMatch" };
                _criteria.IncludeIdPatterns = new[] { "DoesNotMatch.*" };

                // Act
                var actual = _target.IsMatch(_criteria, _package);

                // Assert
                Assert.True(actual, "The package should be matched when none of the rules apply.");
            }

            [Theory]
            [InlineData("NuGet.Versioning", "NuGet.Versioning")]
            [InlineData("NuGet.*", "NuGet.Versioning")]
            [InlineData("NuGet.?ersi?ning", "NuGet.Versioning")]
            [InlineData("NuGet.*ning", "NuGet.Versioning")]
            [InlineData("*.Versioning", "NuGet.Versioning")]
            [InlineData("*", "NuGet.Versioning")]
            [InlineData("N*.?ersi?ning", "NuGet.Versioning")]
            public void PackageIdMatchingSupportsSyntax(string idPattern, string id)
            {
                // Arrange
                _criteria.ExcludeOwners = new string[0];
                _criteria.IncludeIdPatterns = new[] { idPattern };
                _package.PackageRegistration.Id = id;

                // Act
                var actual = _target.IsMatch(_criteria, _package);

                // Assert
                Assert.True(actual, "The package should have been included due to matching ID pattern.");
            }
        }
    }
}
