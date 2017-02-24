// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.SupportRequests.Notifications.Notifications
{
    internal interface INotification
    {
        DateTime ReferenceTime { get; }

        string TemplateName { get; }

        string Subject { get; }

        string TargetEmailAddress { get; }
    }
}