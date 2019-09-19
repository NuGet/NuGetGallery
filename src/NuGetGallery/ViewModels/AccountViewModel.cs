// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    public abstract class AccountViewModel<T> : AccountViewModel where T : User
    {
        public T Account { get; set; }

        public bool IsCertificatesUIEnabled { get; set; }

        public override User User => Account;
    }

    public abstract class AccountViewModel
    {
        public virtual User User { get; }

        public bool IsOrganization => User is Organization;

        public string AccountName { get; set; }

        public bool CanManage { get; set; }

        public bool WasMultiFactorAuthenticated { get; set; }

        public ChangeEmailViewModel ChangeEmail { get; set; }

        public ChangeNotificationsViewModel ChangeNotifications { get; set; }

        public bool HasPassword { get; set; }

        public string CurrentEmailAddress { get; set; }

        public bool HasUnconfirmedEmailAddress { get; set; }

        public bool HasConfirmedEmailAddress { get; set; }

        public bool ProxyGravatar { get; set; }
    }
}
