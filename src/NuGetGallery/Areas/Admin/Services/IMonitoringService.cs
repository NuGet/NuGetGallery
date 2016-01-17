// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGetGallery.Configuration;

namespace NuGetGallery.Areas.Admin
{
    public interface IMonitoringService
    {
        Task TriggerIncident(IAppConfiguration appConfiguration, string errorMessage);
        Task<string> GetPrimaryOnCall(IAppConfiguration appConfiguration);
    }
}