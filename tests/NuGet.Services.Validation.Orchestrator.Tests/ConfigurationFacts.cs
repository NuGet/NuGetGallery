// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ConfigurationFacts
    {
        [Fact]
        public void ConfigurationValidatorSmokeTest()
        {
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation2" }
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation2",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>()
                    }
                }
            };

            var ex = Record.Exception(() => Validate(configuration));

            Assert.Null(ex);
        }

        [Fact]
        public void ConfigurationValidatorDetectsDuplicates()
        {
            const string validationName = "Validation1";
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = validationName,
                        FailAfter = TimeSpan.FromHours(1),
                    },
                    new ValidationConfigurationItem
                    {
                        Name = validationName,
                        FailAfter = TimeSpan.FromHours(1),
                    }
                }
            };

            var ex = Record.Exception(() => Validate(configuration));

            Assert.IsType<ConfigurationErrorsException>(ex);
            Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(validationName, ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConfigurationValidatorDetectsUnknownValidationPrerequisites()
        {
            const string NonExistentValidationName = "SomeValidation";
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ NonExistentValidationName }
                    },
                }
            };

            var ex = Record.Exception(() => Validate(configuration));

            Assert.IsType<ConfigurationErrorsException>(ex);
            Assert.Contains(NonExistentValidationName, ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConfigurationValidatorDetectsLoops()
        {

            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation2"}
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation2",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation1"}
                    }
                }
            };

            var ex = Record.Exception(() => Validate(configuration));

            Assert.IsType<ConfigurationErrorsException>(ex);
            Assert.Contains("loop", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConfigurationValidatorDetectsSelfReferencingValidation()
        {
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation1" }
                    },
                }
            };

            var ex = Record.Exception(() => Validate(configuration));

            Assert.IsType<ConfigurationErrorsException>(ex);
            Assert.Contains("loop", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConfigurationValidatorDetectsSelfReferencingValidation2()
        {
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation2" }
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation2",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation2" }
                    }
                }
            };

            var ex = Record.Exception(() => Validate(configuration));

            Assert.IsType<ConfigurationErrorsException>(ex);
            Assert.Contains("loop", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidationNamesCantBeEmpty()
        {
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "",
                        FailAfter = TimeSpan.FromHours(1)
                    }
                }
            };

            var ex = Record.Exception(() => Validate(configuration));

            Assert.IsType<ConfigurationErrorsException>(ex);
            Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void FailureTimeoutsCantBeZero()
        {
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "SomeValidation",
                        FailAfter = TimeSpan.Zero
                    }
                }
            };

            var ex = Record.Exception(() => Validate(configuration));

            Assert.IsType<ConfigurationErrorsException>(ex);
            Assert.Contains("FailAfter", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConfigurationValidatorTreatsDepencyGraphAsOriented()
        {
            /*                       Validation1
             *                         /     \
             *                        /       \
             *                       v         v
             *               Validation3     Validation4
             *                       ^         ^
             *                        \       /
             *                         \     /
             *                       Validation2
             */

            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation3", "Validation4" }
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation2",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation3", "Validation4" }
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation3",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>()
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation4",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>()
                    }
                }
            };

            var ex = Record.Exception(() => Validate(configuration));

            Assert.Null(ex);
        }

        [Fact]
        public void ConfigurationValidatorTreatsDepencyGraphAsOriented2()
        {
            /*                       Validation1
             *                         /     \
             *                        /       \
             *                       v         v
             *               Validation2     Validation3
             *                       \         /
             *                        \       /
             *                         v     v
             *                       Validation4
             */
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation2", "Validation3" }
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation2",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation4" }
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation3",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation4" }
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation4",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>()
                    }
                }
            };

            var ex = Record.Exception(() => Validate(configuration));

            Assert.Null(ex);
        }

        [Fact]
        public void ValidationOrderingDoesNotAffectLoopDetection()
        {
            // Validation3 ---> Validation1 ---> Validation2
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation2" }
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation2",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>()
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation3",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation1" }
                    }
                }
            };

            var ex = Record.Exception(() => Validate(configuration));

            Assert.Null(ex);
        }

        [Fact]
        public void ConfigurationValidatorBehavesWellOnUnconnectedGraph()
        {
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>()
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation2",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>()
                    }
                }
            };

            var ex = Record.Exception(() => Validate(configuration));

            Assert.Null(ex);
        }

        [Fact]
        public void ConfigurationValidatorDetectsLoopInUnconnectedGraphs()
        {
            /*  Validation1           Validation2 ---> Validation3
             *                                 ^       /
             *                                  \     /
             *                                   -----
             */
            var configuration = new ValidationConfiguration()
            {
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem
                    {
                        Name = "Validation1",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>()
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation2",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation3" }
                    },
                    new ValidationConfigurationItem
                    {
                        Name = "Validation3",
                        FailAfter = TimeSpan.FromHours(1),
                        RequiredValidations = new List<string>{ "Validation2" }
                    }
                }
            };

            var ex = Record.Exception(() => Validate(configuration));

            Assert.Contains("loop", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        private static void Validate(ValidationConfiguration configuration)
        {
            var optionsAccessor = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            optionsAccessor.SetupGet(cfg => cfg.Value).Returns(configuration);
            var validator = new ConfigurationValidator(optionsAccessor.Object);
            validator.Validate();
        }
    }
}
