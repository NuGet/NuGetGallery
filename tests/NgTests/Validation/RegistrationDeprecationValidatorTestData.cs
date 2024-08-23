// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Monitoring.Model;
using NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Registration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NgTests.Validation
{
    public class RegistrationDeprecationValidatorTestData : RegistrationIndexValidatorTestData<RegistrationDeprecationValidator>
    {
        protected override RegistrationDeprecationValidator CreateValidator(
            ILogger<RegistrationDeprecationValidator> logger)
        {
            var endpoint = ValidatorTestUtility.CreateRegistrationEndpoint();
            var config = new ValidatorConfiguration("https://nuget.test/packages", requireRepositorySignature: false);

            return new RegistrationDeprecationValidator(endpoint, config, logger);
        }

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateIndexes
        {
            get
            {
                yield return () => new PackageRegistrationIndexMetadata { Deprecation = null };

                foreach (var reasons in GetPossibleDeprecationReasonCombinations())
                {
                    foreach (var hasMessage in new[] { false, true })
                    {
                        yield return () => new PackageRegistrationIndexMetadata
                        {
                            Deprecation = new PackageRegistrationDeprecationMetadata
                            {
                                Reasons = reasons,
                                Message = hasMessage ? "this is the message" : null
                            }
                        };

                        yield return () => new PackageRegistrationIndexMetadata
                        {
                            Deprecation = new PackageRegistrationDeprecationMetadata
                            {
                                Reasons = reasons,
                                Message = hasMessage ? "this is the message" : null,
                                AlternatePackage = new PackageRegistrationAlternatePackageMetadata
                                {
                                    Id = "thePackage",
                                    Range = "*"
                                }
                            }
                        };

                        yield return () => new PackageRegistrationIndexMetadata
                        {
                            Deprecation = new PackageRegistrationDeprecationMetadata
                            {
                                Reasons = reasons,
                                Message = hasMessage ? "this is the message" : null,
                                AlternatePackage = new PackageRegistrationAlternatePackageMetadata
                                {
                                    Id = "thePackage",
                                    Range = "[1.0.0,1.0.0]"
                                }
                            }
                        };
                    }
                }
            }
        }

        public override IEnumerable<Func<Tuple<PackageRegistrationIndexMetadata, PackageRegistrationIndexMetadata, bool>>> CreateSpecialIndexes
        {
            get
            {
                // Multiple reasons can be in different orders
                foreach (var reasons in GetPossibleDeprecationReasonCombinations().Where(c => c.Count() > 1))
                {
                    yield return () => Tuple.Create(
                        new PackageRegistrationIndexMetadata
                        {
                            Deprecation = new PackageRegistrationDeprecationMetadata
                            {
                                Reasons = reasons.OrderBy(r => r).ToList()
                            }
                        },
                        new PackageRegistrationIndexMetadata
                        {
                            Deprecation = new PackageRegistrationDeprecationMetadata
                            {
                                Reasons = reasons.OrderByDescending(r => r).ToList()
                            }
                        },
                        true);
                }
            }
        }

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateSkippedIndexes => new Func<PackageRegistrationIndexMetadata>[]
        {
            () => null
        };

        private static IEnumerable<IEnumerable<string>> GetPossibleDeprecationReasonCombinations()
        {
            return GetNextPossibleDeprecationReasonCombinations(
                Enum
                    .GetValues(typeof(PackageDeprecationStatus))
                    .Cast<PackageDeprecationStatus>()
                    .Where(s => s != PackageDeprecationStatus.NotDeprecated));
        }

        private static IEnumerable<IEnumerable<string>> GetNextPossibleDeprecationReasonCombinations(IEnumerable<PackageDeprecationStatus> remainingStatuses)
        {
            if (!remainingStatuses.Any())
            {
                yield break;
            }

            var nextStatus = remainingStatuses.First().ToString();
            var nextPossibleStatuses = GetNextPossibleDeprecationReasonCombinations(remainingStatuses.Skip(1));
            if (nextPossibleStatuses.Any())
            {
                foreach (var nextPossibleStatus in nextPossibleStatuses)
                {
                    yield return nextPossibleStatus;
                    yield return nextPossibleStatus.Concat(new[] { nextStatus });
                }
            }

            yield return new[] { nextStatus };
        }
    }
}
