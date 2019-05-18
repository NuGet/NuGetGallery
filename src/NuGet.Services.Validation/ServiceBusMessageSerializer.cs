// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation
{
    public class ServiceBusMessageSerializer : IServiceBusMessageSerializer
    {
        private const string ProcessValidationSetSchemaName = "PackageValidationMessageData";
        private const string CheckValidatorSchemaName = "PackageValidationCheckValidatorMessageData";

        private static readonly BrokeredMessageSerializer<ProcessValidationSetData1> _processValidationSetSerializer = new BrokeredMessageSerializer<ProcessValidationSetData1>();
        private static readonly BrokeredMessageSerializer<CheckValidatorData1> _checkValidatorSerializer = new BrokeredMessageSerializer<CheckValidatorData1>();

        public IBrokeredMessage SerializePackageValidationMessageData(PackageValidationMessageData message)
        {
            switch (message.Type)
            {
                case PackageValidationMessageType.ProcessValidationSet:
                    return _processValidationSetSerializer.Serialize(new ProcessValidationSetData1
                    {
                        PackageId = message.ProcessValidationSet.PackageId,
                        PackageVersion = message.ProcessValidationSet.PackageVersion,
                        PackageNormalizedVersion = message.ProcessValidationSet.PackageNormalizedVersion,
                        ValidationTrackingId = message.ProcessValidationSet.ValidationTrackingId,
                        ValidatingType = message.ProcessValidationSet.ValidatingType,
                        EntityKey = message.ProcessValidationSet.EntityKey,
                    });
                case PackageValidationMessageType.CheckValidator:
                    return _checkValidatorSerializer.Serialize(new CheckValidatorData1
                    {
                        ValidationId = message.CheckValidator.ValidationId,
                    });
                default:
                    throw new NotSupportedException($"The package validation message type '{message.Type}' is not supported.");
            }
        }

        public PackageValidationMessageData DeserializePackageValidationMessageData(IBrokeredMessage message)
        {
            var schemaName = message.GetSchemaName();
            switch (schemaName)
            {
                case ProcessValidationSetSchemaName:
                    var processValidationSet = _processValidationSetSerializer.Deserialize(message);
                    return new PackageValidationMessageData(
                        PackageValidationMessageType.ProcessValidationSet,
                        processValidationSet: new ProcessValidationSetData(
                            processValidationSet.PackageId,
                            processValidationSet.PackageVersion,
                            processValidationSet.ValidationTrackingId,
                            processValidationSet.ValidatingType,
                            processValidationSet.EntityKey),
                        checkValidator: null,
                        deliveryCount: message.DeliveryCount);
                case CheckValidatorSchemaName:
                    var checkValidator = _checkValidatorSerializer.Deserialize(message);
                    return new PackageValidationMessageData(
                        PackageValidationMessageType.CheckValidator,
                        processValidationSet: null,
                        checkValidator: new CheckValidatorData(checkValidator.ValidationId),
                        deliveryCount: message.DeliveryCount);
                default:
                    throw new FormatException($"The provided schema name '{schemaName}' is not supported.");
            }
        }

        [Schema(Name = ProcessValidationSetSchemaName, Version = 1)]
        private class ProcessValidationSetData1
        {
            public string PackageId { get; set; }
            public string PackageVersion { get; set; }
            public string PackageNormalizedVersion { get; set; }
            public Guid ValidationTrackingId { get; set; }
            public ValidatingType ValidatingType { get; set; }
            public int? EntityKey { get; set; }
        }

        [Schema(Name = CheckValidatorSchemaName, Version = 1)]
        private class CheckValidatorData1
        {
            public Guid ValidationId { get; set; }
        }
    }
}
