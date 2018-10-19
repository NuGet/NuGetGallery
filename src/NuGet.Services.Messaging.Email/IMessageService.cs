// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Messaging.Email
{
    public interface IMessageService
    {
        /// <summary>
        /// Sends the message using the provided <see cref="IEmailBuilder"/>.
        /// </summary>
        /// <param name="emailBuilder">The <see cref="IEmailBuilder"/> that will create the message.</param>
        /// <param name="copySender"><c>True</c> to send a copy to the sender; otherwise <c>false</c>.</param>
        /// <param name="discloseSenderAddress">
        /// <c>True</c> if the sender's email address may be disclosed; otherwise <c>false</c>. 
        /// If <c>false</c> and <paramref name="copySender"/> is <c>true</c>, a separate email will be sent to the sender.
        /// </param>
        Task SendMessageAsync(IEmailBuilder emailBuilder, bool copySender = false, bool discloseSenderAddress = false);
    }
}