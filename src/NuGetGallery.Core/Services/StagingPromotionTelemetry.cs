// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Records committed publication and latency from the promotion's persisted acceptance time.
    /// </summary>
    public static class StagingPromotionTelemetry
    {
        public static void TrackGalleryPublished(ILogger logger, NuGet.Services.Logging.ITelemetryClient telemetryClient,
            Guid promotionId, Package package, DateTime? promotionAcceptedAt, bool symbols)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (telemetryClient == null)
            {
                throw new ArgumentNullException(nameof(telemetryClient));
            }

            if (promotionId == Guid.Empty)
            {
                throw new ArgumentException("A promotion ID is required.", nameof(promotionId));
            }

            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (promotionAcceptedAt.HasValue)
            {
                promotionAcceptedAt = DateTime.SpecifyKind(promotionAcceptedAt.Value, DateTimeKind.Utc);
            }

            var publishedAt = DateTime.UtcNow;
            var artifactType = symbols ? "Symbols" : "Package";
            const string eventName = "StagingPromotion.GalleryPublished";
            logger.LogInformation(new EventId(0, eventName), "{PublicationMilestone:l}", JsonConvert.SerializeObject(new
            {
                EventName = eventName,
                PromotionId = promotionId,
                PackageId = package.PackageRegistration.Id,
                PackageVersion = package.NormalizedVersion,
                ArtifactType = artifactType,
                PromotionAcceptedAt = promotionAcceptedAt,
                OccurredAt = publishedAt,
            }));

            if (!promotionAcceptedAt.HasValue)
            {
                logger.LogWarning("Published promotion {PromotionId} has no acceptance timestamp; publication latency is unavailable.", promotionId);
            }
            else if (promotionAcceptedAt.Value > publishedAt)
            {
                logger.LogWarning("Published promotion {PromotionId} has a future acceptance timestamp; check worker clocks before measuring latency.", promotionId);
            }
            else
            {
                telemetryClient.TrackMetric("StagingPromotion.AcceptedToGallerySeconds", (publishedAt - promotionAcceptedAt.Value).TotalSeconds,
                    new Dictionary<string, string> { { "ArtifactType", artifactType } });
            }
        }
    }
}
