// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class Instance
    {
        public Instance(string slot, int index, string diagUrl, string baseQueryUrl, string region, ServiceType serviceType)
        {
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
            Index = index;
            DiagUrl = diagUrl ?? throw new ArgumentNullException(nameof(diagUrl));
            BaseQueryUrl = baseQueryUrl ?? throw new ArgumentNullException(nameof(baseQueryUrl));
            Region = region ?? throw new ArgumentNullException(nameof(region));
            ServiceType = serviceType;
        }

        public string Slot { get; }
        public int Index { get; }
        public string DiagUrl { get; }
        public string BaseQueryUrl { get; }
        public string Region { get; }
        public ServiceType ServiceType { get; }
    }
}
