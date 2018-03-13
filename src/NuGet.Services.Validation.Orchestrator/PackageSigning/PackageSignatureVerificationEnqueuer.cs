// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.PackageSigning
{
    /// <summary>
    /// Kicks off package signature verifications.
    /// </summary>
    public class PackageSignatureVerificationEnqueuer : IPackageSignatureVerificationEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly IBrokeredMessageSerializer<SignatureValidationMessage> _serializer;
        private readonly IOptionsSnapshot<PackageSigningConfiguration> _configuration;

        public PackageSignatureVerificationEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<SignatureValidationMessage> serializer,
            IOptionsSnapshot<PackageSigningConfiguration> configuration)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Kicks off the package verification process for the given request. Verification will begin when the
        /// <see cref="ValidationEntitiesContext"/> has a <see cref="ValidatorStatus"/> that matches the
        /// <see cref="IValidationRequest"/>'s validationId. Once verification completes, the <see cref="ValidatorStatus"/>'s
        /// State will be updated to "Succeeded" or "Failed".
        /// </summary>
        /// <param name="request">The request that details the package to be verified.</param>
        /// <returns>A task that will complete when the verification process has been queued.</returns>
        public Task EnqueueVerificationAsync(IValidationRequest request)
        {
            var message = new SignatureValidationMessage(
                request.PackageId,
                request.PackageVersion,
                new Uri(request.NupkgUrl),
                request.ValidationId);
            var brokeredMessage = _serializer.Serialize(message);

            var visibleAt = DateTimeOffset.UtcNow + (_configuration.Value.MessageDelay ?? TimeSpan.Zero);
            brokeredMessage.ScheduledEnqueueTimeUtc = visibleAt;

            return _topicClient.SendAsync(brokeredMessage);
        }
    }
}
