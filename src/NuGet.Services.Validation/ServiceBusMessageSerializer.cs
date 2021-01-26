// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation
{
    public class ServiceBusMessageSerializer : IServiceBusMessageSerializer
    {
        private const string ProcessValidationSetSchemaName = "PackageValidationMessageData";
        private const string CheckValidatorSchemaName = "PackageValidationCheckValidatorMessageData";

        private const string StartValidationSchemaName = "StartValidation";
        private const string CheckValidationSetSchemaName = "CheckValidationSet";

        private static readonly BrokeredMessageSerializer<StartValidationData1> _startValidationSerializer = new BrokeredMessageSerializer<StartValidationData1>();
        private static readonly BrokeredMessageSerializer<ProcessValidationSetData1> _processValidationSetSerializer = new BrokeredMessageSerializer<ProcessValidationSetData1>();
        private static readonly BrokeredMessageSerializer<CheckValidationSetData1> _checkValidationSetSerializer = new BrokeredMessageSerializer<CheckValidationSetData1>();
        private static readonly BrokeredMessageSerializer<CheckValidatorData1> _checkValidatorSerializer = new BrokeredMessageSerializer<CheckValidatorData1>();

        public IBrokeredMessage SerializePackageValidationMessageData(PackageValidationMessageData message)
        {
            switch (message.Type)
            {
                case PackageValidationMessageType.StartValidation:
                    return _startValidationSerializer.Serialize(new StartValidationData1
                    {
                        ValidationTrackingId = message.StartValidation.ValidationTrackingId,
                        ContentType = message.StartValidation.ContentType,
                        ContentUrl = message.StartValidation.ContentUrl,
                        Properties = message.StartValidation.Properties,
                    });
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
                case PackageValidationMessageType.CheckValidationSet:
                    return _checkValidationSetSerializer.Serialize(new CheckValidationSetData1
                    {
                        ValidationTrackingId = message.CheckValidationSet.ValidationTrackingId,
                        ExtendExpiration = message.CheckValidationSet.ExtendExpiration,
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
                case StartValidationSchemaName:
                    var startValidation = _startValidationSerializer.Deserialize(message);
                    return new PackageValidationMessageData(
                        PackageValidationMessageType.StartValidation,
                        startValidation: new StartValidationData(
                            startValidation.ValidationTrackingId,
                            startValidation.ContentType,
                            startValidation.ContentUrl,
                            startValidation.Properties),
                        processValidationSet: null,
                        checkValidationSet: null,
                        checkValidator: null,
                        deliveryCount: message.DeliveryCount);
                case ProcessValidationSetSchemaName:
                    var processValidationSet = _processValidationSetSerializer.Deserialize(message);
                    return new PackageValidationMessageData(
                        PackageValidationMessageType.ProcessValidationSet,
                        startValidation: null,
                        processValidationSet: new ProcessValidationSetData(
                            processValidationSet.PackageId,
                            processValidationSet.PackageVersion,
                            processValidationSet.ValidationTrackingId,
                            processValidationSet.ValidatingType,
                            processValidationSet.EntityKey),
                        checkValidationSet: null,
                        checkValidator: null,
                        deliveryCount: message.DeliveryCount);
                case CheckValidationSetSchemaName:
                    var checkValidationSet = _checkValidationSetSerializer.Deserialize(message);
                    return new PackageValidationMessageData(
                        PackageValidationMessageType.CheckValidationSet,
                        startValidation: null,
                        processValidationSet: null,
                        checkValidationSet: new CheckValidationSetData(
                            checkValidationSet.ValidationTrackingId,
                            checkValidationSet.ExtendExpiration),
                        checkValidator: null,
                        deliveryCount: message.DeliveryCount);
                case CheckValidatorSchemaName:
                    var checkValidator = _checkValidatorSerializer.Deserialize(message);
                    return new PackageValidationMessageData(
                        PackageValidationMessageType.CheckValidator,
                        startValidation: null,
                        processValidationSet: null,
                        checkValidationSet: null,
                        checkValidator: new CheckValidatorData(checkValidator.ValidationId),
                        deliveryCount: message.DeliveryCount);
                default:
                    throw new FormatException($"The provided schema name '{schemaName}' is not supported.");
            }
        }

        [Schema(Name = StartValidationSchemaName, Version = 1)]
        private class StartValidationData1
        {
            public Guid ValidationTrackingId { get; set; }
            public string ContentType { get; set; }
            public Uri ContentUrl { get; set; }
            public JObject Properties { get; set; }
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

        [Schema(Name = CheckValidationSetSchemaName, Version = 1)]
        private class CheckValidationSetData1
        {
            public Guid ValidationTrackingId { get; set; }
            public bool ExtendExpiration { get; set; }
        }

        [Schema(Name = CheckValidatorSchemaName, Version = 1)]
        private class CheckValidatorData1
        {
            public Guid ValidationId { get; set; }
        }
    }
}
