// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.Common
{
    public interface INotificationService
    {
        Task SendNotificationAsync(string subject, string body);
        Task SendNotificationAsync(string category, string subject, string body);
    }
}