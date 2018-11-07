// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageValidatorFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ValidationCollectorFactory> _logger;

        public PackageValidatorFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<ValidationCollectorFactory>();
        }

        public PackageValidator Create(
            string galleryUrl,
            string indexUrl,
            StorageFactory auditingStorageFactory,
            IEnumerable<EndpointFactory.Input> endpointInputs,
            Func<HttpMessageHandler> messageHandlerFactory,
            ValidatorConfiguration validatorConfig,
            bool verbose = false)
        {
            var validatorFactory = new ValidatorFactoryFactory(validatorConfig, _loggerFactory).Create(galleryUrl, indexUrl);
            var endpointFactory = new EndpointFactory(validatorFactory, messageHandlerFactory, _loggerFactory);

            var validators = new List<IAggregateValidator>();

            validators.AddRange(endpointInputs.Select(e => endpointFactory.Create(e)));
            validators.Add(new CatalogAggregateValidator(validatorFactory, validatorConfig));

            return new PackageValidator(validators, auditingStorageFactory, _loggerFactory.CreateLogger<PackageValidator>());
        }
    }
}