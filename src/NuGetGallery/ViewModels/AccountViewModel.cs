// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public abstract class AccountViewModel
    {
        public IList<string> CuratedFeeds { get; set; }

        public ChangeEmailViewModel ChangeEmail { get; set; }

        public ChangeNotificationsViewModel ChangeNotifications { get; set; }

        public bool HasPassword { get; set; }

        public string CurrentEmailAddress { get; set; }

        public bool HasUnconfirmedEmailAddress { get; set; }

        public bool HasConfirmedEmailAddress { get; set; }
    }
}
