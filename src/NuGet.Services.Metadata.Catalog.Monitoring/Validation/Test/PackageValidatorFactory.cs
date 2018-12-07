// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageValidatorFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public PackageValidatorFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
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
            if (string.IsNullOrEmpty(galleryUrl))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(galleryUrl));
            }

            if (string.IsNullOrEmpty(indexUrl))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(indexUrl));
            }

            if (auditingStorageFactory == null)
            {
                throw new ArgumentNullException(nameof(auditingStorageFactory));
            }

            if (endpointInputs == null)
            {
                throw new ArgumentNullException(nameof(endpointInputs));
            }

            if (messageHandlerFactory == null)
            {
                throw new ArgumentNullException(nameof(messageHandlerFactory));
            }

            if (validatorConfig == null)
            {
                throw new ArgumentNullException(nameof(validatorConfig));
            }

            var validatorFactory = new ValidatorFactory(validatorConfig, _loggerFactory);
            var endpointFactory = new EndpointFactory(validatorFactory, messageHandlerFactory, _loggerFactory);

            var validators = new List<IAggregateValidator>();

            validators.AddRange(endpointInputs.Select(e => endpointFactory.Create(e)));
            validators.Add(new CatalogAggregateValidator(validatorFactory, validatorConfig));

            var feedToSource = new Dictionary<FeedType, SourceRepository>()
            {
                { FeedType.HttpV2, new SourceRepository(new PackageSource(galleryUrl), GetResourceProviders(ResourceProvidersToInjectV2), FeedType.HttpV2) },
                { FeedType.HttpV3, new SourceRepository(new PackageSource(indexUrl), GetResourceProviders(ResourceProvidersToInjectV3), FeedType.HttpV3) }
            };

            return new PackageValidator(
                validators,
                auditingStorageFactory,
                feedToSource,
                _loggerFactory.CreateLogger<PackageValidator>(),
                _loggerFactory.CreateLogger<ValidationContext>());
        }

        private IEnumerable<Lazy<INuGetResourceProvider>> ResourceProvidersToInjectV2 => new Lazy<INuGetResourceProvider>[]
        {
            new Lazy<INuGetResourceProvider>(() => new NonhijackableV2HttpHandlerResourceProvider()),
            new Lazy<INuGetResourceProvider>(() => new PackageTimestampMetadataResourceV2Provider(_loggerFactory)),
            new Lazy<INuGetResourceProvider>(() => new PackageRegistrationMetadataResourceV2FeedProvider())
        };

        private IEnumerable<Lazy<INuGetResourceProvider>> ResourceProvidersToInjectV3 => new Lazy<INuGetResourceProvider>[]
        {
            new Lazy<INuGetResourceProvider>(() => new PackageRegistrationMetadataResourceV3Provider())
        };

        private IEnumerable<Lazy<INuGetResourceProvider>> GetResourceProviders(IEnumerable<Lazy<INuGetResourceProvider>> providersToInject)
        {
            var resourceProviders = Repository.Provider.GetCoreV3().ToList();

            resourceProviders.AddRange(providersToInject);

            return resourceProviders;
        }
    }
}