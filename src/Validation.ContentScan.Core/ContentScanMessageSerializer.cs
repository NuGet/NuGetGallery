// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation.ContentScan
{
    public class ContentScanMessageSerializer : IBrokeredMessageSerializer<ContentScanData>
    {
        private const string StartScanSchemaName = "StartContentScanData";
        private const string CheckScanStatusSchemaName = "CheckContentScanStatusData";

        private IBrokeredMessageSerializer<StartContentScanData1> _startScanSerializer =
            new BrokeredMessageSerializer<StartContentScanData1>();
        private IBrokeredMessageSerializer<CheckContentScanStatusData1> _checkScanStatusSerializer =
            new BrokeredMessageSerializer<CheckContentScanStatusData1>();

        public ContentScanData Deserialize(IReceivedBrokeredMessage message)
        {
            var schemaName = message.GetSchemaName();
            switch (schemaName)
            {
                case StartScanSchemaName:
                    var startContentScan = _startScanSerializer.Deserialize(message);
                    return new ContentScanData(
                        ContentScanOperationType.StartScan,
                        startContentScan: new StartContentScanData(
                            startContentScan.ValidationStepId,
                            startContentScan.BlobUri),
                        checkContentScanStatus: null,
                        deliveryCount: message.DeliveryCount);
                case CheckScanStatusSchemaName:
                    var checkScanStatus = _checkScanStatusSerializer.Deserialize(message);
                    return new ContentScanData(
                        ContentScanOperationType.CheckStatus,
                        startContentScan: null,
                        checkContentScanStatus: new CheckContentScanStatusData(
                           checkScanStatus.ValidationStepId),
                        deliveryCount: message.DeliveryCount);
                default:
                    throw new FormatException($"The provided schema name '{schemaName}' is not supported.");
            }
        }

        public IBrokeredMessage Serialize(ContentScanData message)
        {
            switch (message.Type)
            {
                case ContentScanOperationType.StartScan:
                    return _startScanSerializer.Serialize(new StartContentScanData1
                    {
                        ValidationStepId = message.StartContentScan.ValidationStepId,
                        BlobUri = message.StartContentScan.BlobUri
                    });
                case ContentScanOperationType.CheckStatus:
                    return _checkScanStatusSerializer.Serialize(new CheckContentScanStatusData1
                    {
                        ValidationStepId = message.CheckContentScanStatus.ValidationStepId,
                    });
                default:
                    throw new NotSupportedException($"The Content Scan message type '{message.Type}' is not supported.");
            }
        }

        [Schema(Name = StartScanSchemaName, Version = 1)]
        private class StartContentScanData1
        {
            public Guid ValidationStepId { get; set; }
            public Uri BlobUri { get; set; }
            public string ContentType { get; set; }
        }

        [Schema(Name = CheckScanStatusSchemaName, Version = 1)]
        private class CheckContentScanStatusData1
        {
            public Guid ValidationStepId { get; set; }
        }
    }
}
