// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Validation
{
    public class PackageValidationMessageData
    {
        public static PackageValidationMessageData NewStartValidation(
            Guid validationTrackingId,
            string contentType,
            Uri contentUrl,
            JObject properties)
        {
            return new PackageValidationMessageData(
                PackageValidationMessageType.StartValidation,
                startValidation: new StartValidationData(
                    validationTrackingId,
                    contentType,
                    contentUrl,
                    properties),
                processValidationSet: null,
                checkValidationSet: null,
                checkValidator: null,
                deliveryCount: 0);
        }

        public static PackageValidationMessageData NewProcessValidationSet(
            string packageId,
            string packageVersion,
            Guid validationTrackingId,
            ValidatingType validatingType,
            int? entityKey)
        {
            return new PackageValidationMessageData(
                PackageValidationMessageType.ProcessValidationSet,
                startValidation: null,
                processValidationSet: new ProcessValidationSetData(
                    packageId,
                    packageVersion,
                    validationTrackingId,
                    validatingType,
                    entityKey),
                checkValidationSet: null,
                checkValidator: null,
                deliveryCount: 0);
        }

        public static PackageValidationMessageData NewCheckValidationSet(Guid validationTrackingId, bool extendExpiration)
        {
            return new PackageValidationMessageData(
                PackageValidationMessageType.CheckValidationSet,
                startValidation: null,
                processValidationSet: null,
                checkValidationSet: new CheckValidationSetData(validationTrackingId, extendExpiration),
                checkValidator: null,
                deliveryCount: 0);
        }

        public static PackageValidationMessageData NewCheckValidator(Guid validationId)
        {
            return new PackageValidationMessageData(
                PackageValidationMessageType.CheckValidator,
                startValidation: null,
                processValidationSet: null,
                checkValidationSet: null,
                checkValidator: new CheckValidatorData(validationId),
                deliveryCount: 0);
        }

        internal PackageValidationMessageData(
            PackageValidationMessageType type,
            StartValidationData startValidation,
            ProcessValidationSetData processValidationSet,
            CheckValidationSetData checkValidationSet,
            CheckValidatorData checkValidator,
            int deliveryCount)
        {
            switch (type)
            {
                case PackageValidationMessageType.StartValidation:
                    if (startValidation == null)
                    {
                        throw new ArgumentNullException(nameof(startValidation));
                    }
                    break;
                case PackageValidationMessageType.ProcessValidationSet:
                    if (processValidationSet == null)
                    {
                        throw new ArgumentNullException(nameof(processValidationSet));
                    }
                    break;
                case PackageValidationMessageType.CheckValidationSet:
                    if (checkValidationSet == null)
                    {
                        throw new ArgumentNullException(nameof(checkValidationSet));
                    }
                    break;
                case PackageValidationMessageType.CheckValidator:
                    if (checkValidator == null)
                    {
                        throw new ArgumentNullException(nameof(checkValidator));
                    }
                    break;
                default:
                    throw new NotSupportedException($"The package validation message type '{type}' is not supported.");
            }

            var notNullCount = 0;
            notNullCount += startValidation != null ? 1 : 0;
            notNullCount += processValidationSet != null ? 1 : 0;
            notNullCount += checkValidationSet != null ? 1 : 0;
            notNullCount += checkValidator != null ? 1 : 0;
            if (notNullCount > 1)
            {
                throw new ArgumentException("There should be exactly one non-null data instance provided.");
            }

            Type = type;
            StartValidation = startValidation;
            ProcessValidationSet = processValidationSet;
            CheckValidationSet = checkValidationSet;
            CheckValidator = checkValidator;
            DeliveryCount = deliveryCount;
        }

        public PackageValidationMessageType Type { get; }
        public StartValidationData StartValidation { get; }
        public ProcessValidationSetData ProcessValidationSet { get; }
        public CheckValidationSetData CheckValidationSet { get; }
        public CheckValidatorData CheckValidator { get; }
        public int DeliveryCount { get; }
    }
}
