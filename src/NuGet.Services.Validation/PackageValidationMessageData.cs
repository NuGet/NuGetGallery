// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
    public class PackageValidationMessageData
    {
        public static PackageValidationMessageData NewProcessValidationSet(
            string packageId,
            string packageVersion,
            Guid validationTrackingId,
            ValidatingType validatingType,
            int? entityKey)
        {
            return new PackageValidationMessageData(
                PackageValidationMessageType.ProcessValidationSet,
                processValidationSet: new ProcessValidationSetData(
                    packageId,
                    packageVersion,
                    validationTrackingId,
                    validatingType,
                    entityKey),
                checkValidator: null,
                deliveryCount: 0);
        }

        public static PackageValidationMessageData NewCheckValidator(Guid validationId)
        {
            return new PackageValidationMessageData(
                PackageValidationMessageType.CheckValidator,
                processValidationSet: null,
                checkValidator: new CheckValidatorData(validationId),
                deliveryCount: 0);
        }

        internal PackageValidationMessageData(
            PackageValidationMessageType type,
            ProcessValidationSetData processValidationSet,
            CheckValidatorData checkValidator,
            int deliveryCount)
        {
            switch (type)
            {
                case PackageValidationMessageType.ProcessValidationSet:
                    if (processValidationSet == null)
                    {
                        throw new ArgumentNullException(nameof(processValidationSet));
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
            notNullCount += processValidationSet != null ? 1 : 0;
            notNullCount += checkValidator != null ? 1 : 0;
            if (notNullCount > 1)
            {
                throw new ArgumentException("There should be exactly one non-null data instance provided.");
            }

            Type = type;
            ProcessValidationSet = processValidationSet;
            CheckValidator = checkValidator;
            DeliveryCount = deliveryCount;
        }

        public PackageValidationMessageType Type { get; }
        public ProcessValidationSetData ProcessValidationSet { get; }
        public CheckValidatorData CheckValidator { get; }
        public int DeliveryCount { get; }
    }
}
