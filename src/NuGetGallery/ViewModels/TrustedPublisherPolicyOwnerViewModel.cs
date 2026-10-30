// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class TrustedPublisherPolicyOwnerViewModel
    {
        public TrustedPublisherPolicyOwnerViewModel(string owner, bool canPushNew, bool canPushExisting, bool canUnlist, bool canStage)
        {
            Owner = owner;
            CanPushNew = canPushNew;
            CanPushExisting = canPushExisting;
            CanUnlist = canUnlist;
            CanStage = canStage;
        }

        public string Owner { get; }
        public bool CanPushNew { get; }
        public bool CanPushExisting { get; }
        public bool CanUnlist { get; }
        public bool CanStage { get; }
    }
}
