// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Messaging
{
    public interface IEmailMessageEnqueuer
    {
        /// <summary>
        /// Enqueues publishing of the specified email message to start ASAP.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        Task SendEmailMessageAsync(EmailMessageData message);
    }
}
