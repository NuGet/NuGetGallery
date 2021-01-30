// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.PackageSigning.ProcessSignature
{
    /// <summary>
    /// Kicks off package signature verifications.
    /// </summary>
    public class ProcessSignatureEnqueuer : IProcessSignatureEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly IBrokeredMessageSerializer<SignatureValidationMessage> _serializer;
        private readonly IOptionsSnapshot<ProcessSignatureConfiguration> _configuration;

        public ProcessSignatureEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<SignatureValidationMessage> serializer,
            IOptionsSnapshot<ProcessSignatureConfiguration> configuration)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public Task EnqueueProcessSignatureAsync(INuGetValidationRequest request, bool requireRepositorySignature)
        {
            var message = new SignatureValidationMessage(
                request.PackageId,
                request.PackageVersion,
                new Uri(request.NupkgUrl),
                request.ValidationId,
                requireRepositorySignature);
            var brokeredMessage = _serializer.Serialize(message);

            var visibleAt = DateTimeOffset.UtcNow + (_configuration.Value.MessageDelay ?? TimeSpan.Zero);
            brokeredMessage.ScheduledEnqueueTimeUtc = visibleAt;

            return _topicClient.SendAsync(brokeredMessage);
        }
    }
}
