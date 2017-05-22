// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Associates a set of <see cref="IValidator"/>s with a <see cref="ReadCursor"/>.
    /// The <see cref="ReadCursor"/> represents the latest point in the catalog for which the <see cref="IValidator"/>s should be run.
    /// </summary>
    public abstract class EndpointValidator : AggregateValidator
    {
        public EndpointValidator(ReadCursor cursor, ValidatorFactory factory, ILogger<EndpointValidator> logger)
            : base(factory, logger)
        {
            Cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
        }

        /// <summary>
        /// Entries in the catalog past this cursor should not be tested because the endpoint does not contain them.
        /// </summary>
        public ReadCursor Cursor { get; private set; }

        /// <summary>
        /// Constructs all <see cref="Type"/>s from this assembly that are assignable to <see cref="IValidator{T}"/> and returns them.
        /// </summary>
        protected override IEnumerable<IValidator> GetValidators(ValidatorFactory factory)
        {
            var validationTypes = Assembly.GetExecutingAssembly().GetTypes()
                                    .Where(p => 
                                        typeof(IValidator<>)
                                        .MakeGenericType(GetType())
                                        .IsAssignableFrom(p)
                                        && !p.IsAbstract);

            var validators = new List<IValidator>();

            foreach (var validatorType in validationTypes)
            {
                try
                {
                    var validator = factory.Create(validatorType);
                    if (validator != null)
                    {
                        validators.Add(validator);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(LogEvents.ValidationFailedToInitialize, e, "Failed to construct {ValidationType}!", validatorType.Name);
                }
            }

            return validators;
        }
    }
}
