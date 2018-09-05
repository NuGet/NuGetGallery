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
        private ILoggerFactory _loggerFactory;
        private ILogger<ValidationCollectorFactory> _logger;

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
            bool requireSignature = false,
            bool verbose = false)
        {
            var validatorFactory = new ValidatorFactoryFactory(_loggerFactory).Create(galleryUrl, indexUrl);
            var endpointFactory = new EndpointFactory(validatorFactory, messageHandlerFactory, _loggerFactory);

            var validators = new List<IAggregateValidator>();

            validators.AddRange(endpointInputs.Select(e => endpointFactory.Create(e)));
            validators.Add(new CatalogAggregateValidator(validatorFactory, requireSignature));

            return new PackageValidator(validators, auditingStorageFactory, _loggerFactory.CreateLogger<PackageValidator>());
        }
    }
}