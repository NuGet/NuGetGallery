// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation
{
    public interface IServiceBusMessageSerializer
    {
        PackageValidationMessageData DeserializePackageValidationMessageData(IReceivedBrokeredMessage message);
        IBrokeredMessage SerializePackageValidationMessageData(PackageValidationMessageData message);
    }
}
