// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class TopologicalSortFacts
    {
        public class Validate
        {
            private const string CycleError = "No validation sequences were found. This indicates a cycle in the validation dependencies.";
            private const string ParallelProcessorError = "Processors must not run in parallel with any other validators.";

            [Fact]
            public void ReturnTrueForPath()
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string>() },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string> { "0" } },
                    new ValidationConfigurationItem { Name = "2", RequiredValidations = new List<string> { "1" } },
                };

                // Act
                var error = GetValidationError(validators, new[] { "0", "1", "2" });

                // Assert
                Assert.Null(error);
            }

            [Fact]
            public void SucceedsOnDisconnectedGraphContainingNoProcessor()
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string>() },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string>() },
                };

                // Act
                var error = GetValidationError(validators, new string[0]);

                // Assert
                Assert.Null(error);
            }

            [Fact]
            public void SucceedsOnSingleNodeGraphContainingProcessor()
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string>() },
                };

                // Act
                var error = GetValidationError(validators, new[] { "0" });

                // Assert
                Assert.Null(error);
            }

            [Fact]
            public void SucceedsOnSingleNodeGraphContainingNoProcessor()
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string>() },
                };

                // Act
                var error = GetValidationError(validators, new string[0]);

                // Assert
                Assert.Null(error);
            }

            [Theory]
            [InlineData("0")]
            [InlineData("1")]
            public void FailsOnDisconnectedGraphContainingProcessor(string name)
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string>() },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string>() },
                };

                // Act
                var error = GetValidationError(validators, new[] { name });

                // Assert
                Assert.Contains(ParallelProcessorError, error);
            }

            [Theory]
            [InlineData("0")]
            [InlineData("1")]
            public void ReturnFalseForTwoNodeCycleGraph(string name)
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string> { "1" } },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string> { "0" } },
                };

                // Act
                var error = GetValidationError(validators, new[] { name });

                // Assert
                Assert.Contains(CycleError, error);
            }

            [Fact]
            public void ReturnFalseForThreeNodeCycleGraph()
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string> { "3" } },
                    new ValidationConfigurationItem { Name = "2", RequiredValidations = new List<string> { "1" } },
                    new ValidationConfigurationItem { Name = "3", RequiredValidations = new List<string> { "2" } },
                };

                // Act
                var error = GetValidationError(validators, new string[0]);

                // Assert
                Assert.Contains(CycleError, error);
            }

            [Theory]
            [InlineData("0", false)]
            [InlineData("1", false)]
            [InlineData("2", false)]
            [InlineData("3", true)]
            [InlineData("4", true)]
            [InlineData("5", true)]
            public void GraphShapedLikeY(string name, bool valid)
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string>() },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string>() },
                    new ValidationConfigurationItem { Name = "2", RequiredValidations = new List<string> { "1" } },
                    new ValidationConfigurationItem { Name = "3", RequiredValidations = new List<string> { "0", "2" } },
                    new ValidationConfigurationItem { Name = "4", RequiredValidations = new List<string> { "3" } },
                    new ValidationConfigurationItem { Name = "5", RequiredValidations = new List<string> { "4" } },
                };

                // Act & Assert
                Verify(name, valid, validators);
            }

            [Theory]
            [InlineData("0", true)]
            [InlineData("1", true)]
            [InlineData("2", false)]
            [InlineData("3", false)]
            [InlineData("4", false)]
            [InlineData("5", false)]
            public void GraphShapedLikeUpsideDownY(string name, bool valid)
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string>() },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string> { "0" } },
                    new ValidationConfigurationItem { Name = "2", RequiredValidations = new List<string> { "1" } },
                    new ValidationConfigurationItem { Name = "3", RequiredValidations = new List<string> { "1" } },
                    new ValidationConfigurationItem { Name = "4", RequiredValidations = new List<string> { "2" } },
                    new ValidationConfigurationItem { Name = "5", RequiredValidations = new List<string> { "4" } },
                };

                // Act & Assert
                Verify(name, valid, validators);
            }

            [Theory]
            [InlineData("0", true)]
            [InlineData("1", false)]
            [InlineData("2", false)]
            [InlineData("3", false)]
            [InlineData("4", false)]
            [InlineData("5", false)]
            [InlineData("6", true)]
            [InlineData("7", true)]
            [InlineData("8", false)]
            [InlineData("9", false)]
            public void ComplexGraph(string name, bool valid)
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string>() },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string> { "0" } },
                    new ValidationConfigurationItem { Name = "2", RequiredValidations = new List<string> { "1" } },
                    new ValidationConfigurationItem { Name = "3", RequiredValidations = new List<string> { "0" } },
                    new ValidationConfigurationItem { Name = "4", RequiredValidations = new List<string> { "2", "3" } },
                    new ValidationConfigurationItem { Name = "5", RequiredValidations = new List<string> { "0" } },
                    new ValidationConfigurationItem { Name = "6", RequiredValidations = new List<string> { "4", "5" } },
                    new ValidationConfigurationItem { Name = "7", RequiredValidations = new List<string> { "6" } },
                    new ValidationConfigurationItem { Name = "8", RequiredValidations = new List<string> { "7" } },
                    new ValidationConfigurationItem { Name = "9", RequiredValidations = new List<string> { "7" } },
                };

                // Act & Assert
                Verify(name, valid, validators);
            }

            [Fact]
            public void ComplexCycle()
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string>() },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string> { "0" } },
                    new ValidationConfigurationItem { Name = "2", RequiredValidations = new List<string> { "1" } },
                    new ValidationConfigurationItem { Name = "3", RequiredValidations = new List<string> { "0" } },
                    new ValidationConfigurationItem { Name = "4", RequiredValidations = new List<string> { "2", "3" } },
                    new ValidationConfigurationItem { Name = "5", RequiredValidations = new List<string> { "0", "9" } },
                    new ValidationConfigurationItem { Name = "6", RequiredValidations = new List<string> { "4", "5" } },
                    new ValidationConfigurationItem { Name = "7", RequiredValidations = new List<string> { "6" } },
                    new ValidationConfigurationItem { Name = "8", RequiredValidations = new List<string> { "7" } },
                    new ValidationConfigurationItem { Name = "9", RequiredValidations = new List<string> { "7" } },
                };

                // Act
                var error = GetValidationError(validators, new string[0]);

                // Assert
                Assert.Contains(CycleError, error);
            }

            [Fact]
            public void DisconnectedCycle()
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string>() },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string> { "0" } },
                    new ValidationConfigurationItem { Name = "2", RequiredValidations = new List<string> { "1" } },
                    new ValidationConfigurationItem { Name = "3", RequiredValidations = new List<string> { "5" } },
                    new ValidationConfigurationItem { Name = "4", RequiredValidations = new List<string> { "3" } },
                    new ValidationConfigurationItem { Name = "5", RequiredValidations = new List<string> { "4" } },
                };

                // Act
                var error = GetValidationError(validators, new string[0]);

                // Assert
                Assert.Contains(CycleError, error);
            }

            private static void Verify(string name, bool valid, ValidationConfigurationItem[] validators)
            {
                // Act
                var error = GetValidationError(validators, new[] { name });

                // Assert
                if (valid)
                {
                    Assert.Null(error);
                }
                else
                {
                    Assert.Contains(ParallelProcessorError, error);
                }
            }

            private static string GetValidationError(IReadOnlyList<ValidationConfigurationItem> validators, IReadOnlyList<string> cannotBeParallel)
            {
                try
                {
                    TopologicalSort.Validate(validators, cannotBeParallel);
                    return null;
                }
                catch (ConfigurationErrorsException e)
                {
                    return e.Message;
                }
            }
        }

        public class EnumerateAll
        {
            [Fact]
            public void ReturnsNestedEmptyListEmptyGraph()
            {
                // Arrange
                var validators = new ValidationConfigurationItem[0];

                // Act
                var actual = TopologicalSort.EnumerateAll(validators);

                // Assert
                Assert.Equal(new List<List<string>> { new List<string>() }, actual);
            }

            [Fact]
            public void ReturnsEmptyListForCycle()
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string> { "1" } },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string> { "2" } },
                    new ValidationConfigurationItem { Name = "2", RequiredValidations = new List<string> { "0" } },
                };

                // Act
                var actual = TopologicalSort.EnumerateAll(validators);

                // Assert
                Assert.Empty(actual);
            }

            [Fact]
            public void ReturnsSingleResultForPath()
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string>() },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string> { "0" } },
                    new ValidationConfigurationItem { Name = "2", RequiredValidations = new List<string> { "1" } },
                };

                // Act
                var actual = TopologicalSort.EnumerateAll(validators);

                // Assert
                var result = Assert.Single(actual);
                Assert.Equal(new List<string>() { "0", "1", "2" }, result);
            }

            [Fact]
            public void ReturnsTwoResultsForDiamond()
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string>() },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string> { "0" } },
                    new ValidationConfigurationItem { Name = "2", RequiredValidations = new List<string> { "0" } },
                    new ValidationConfigurationItem { Name = "3", RequiredValidations = new List<string> { "1", "2" } },
                };

                // Act
                var actual = TopologicalSort.EnumerateAll(validators);

                // Assert
                actual = actual.OrderBy(x => string.Join(" ", x)).ToList();
                Assert.Equal(2, actual.Count);
                Assert.Equal(new List<string>() { "0", "1", "2", "3" }, actual[0]);
                Assert.Equal(new List<string>() { "0", "2", "1", "3" }, actual[1]);
            }

            [Fact]
            public void ReturnsTwoResultsForTwoDisconnected()
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string>() },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string>() },
                };

                // Act
                var actual = TopologicalSort.EnumerateAll(validators);

                // Assert
                actual = actual.OrderBy(x => string.Join(" ", x)).ToList();
                Assert.Equal(2, actual.Count);
                Assert.Equal(new List<string>() { "0", "1" }, actual[0]);
                Assert.Equal(new List<string>() { "1", "0" }, actual[1]);
            }

            [Fact]
            public void ThrowsWhenNodeIsMissing()
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string>() },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string> { "2" } },
                };

                // Act
                Assert.Throws<KeyNotFoundException>(() => TopologicalSort.EnumerateAll(validators));
            }

            /// <summary>
            /// Source: https://www.geeksforgeeks.org/all-topological-sorts-of-a-directed-acyclic-graph/
            /// </summary>
            [Fact]
            public void ProducesTutorialOutput()
            {
                // Arrange
                var validators = new[]
                {
                    new ValidationConfigurationItem { Name = "0", RequiredValidations = new List<string> { "4", "5"} },
                    new ValidationConfigurationItem { Name = "1", RequiredValidations = new List<string> { "3", "4"} },
                    new ValidationConfigurationItem { Name = "2", RequiredValidations = new List<string> { "5" } },
                    new ValidationConfigurationItem { Name = "3", RequiredValidations = new List<string> { "2" } },
                    new ValidationConfigurationItem { Name = "4", RequiredValidations = new List<string>() },
                    new ValidationConfigurationItem { Name = "5", RequiredValidations = new List<string>() },
                };

                var expected = new List<List<string>>
                {
                    new List<string> { "4", "5", "0", "2", "3", "1" },
                    new List<string> { "4", "5", "2", "0", "3", "1" },
                    new List<string> { "4", "5", "2", "3", "0", "1" },
                    new List<string> { "4", "5", "2", "3", "1", "0" },
                    new List<string> { "5", "2", "3", "4", "0", "1" },
                    new List<string> { "5", "2", "3", "4", "1", "0" },
                    new List<string> { "5", "2", "4", "0", "3", "1" },
                    new List<string> { "5", "2", "4", "3", "0", "1" },
                    new List<string> { "5", "2", "4", "3", "1", "0" },
                    new List<string> { "5", "4", "0", "2", "3", "1" },
                    new List<string> { "5", "4", "2", "0", "3", "1" },
                    new List<string> { "5", "4", "2", "3", "0", "1" },
                    new List<string> { "5", "4", "2", "3", "1", "0" },
                };

                // Act
                var actual = TopologicalSort.EnumerateAll(validators);

                // Assert
                Assert.Equal(
                     expected.OrderBy(x => string.Join(" ", x)),
                     actual.OrderBy(x => string.Join(" ", x)));
            }
        }
    }
}
