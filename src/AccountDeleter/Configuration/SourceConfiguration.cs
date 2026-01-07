// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// Used to configure how to respond to a message source.
    /// </summary>
    public class SourceConfiguration
    {
        /// <summary>
        /// Name of the message source that this configuration is for. We use these values to figure out known message sources.
        /// </summary>
        public string SourceName { get; set; }

        /// <summary>
        /// Indicates if a user should be notified when a message from this source successfully deletes an account.
        /// </summary>
        public bool SendMessageOnSuccess { get; set; }

        /// <summary>
        /// Mail template for if a user cannot be deleted automatically and must be notified.
        /// </summary>
        public MailTemplateConfiguration NotifyMailTemplate { get; set; }

        /// <summary>
        /// Mail template for notifying a user that the account has been deleted.
        /// </summary>
        public MailTemplateConfiguration DeletedMailTemplate { get; set; }

        /// <summary>
        /// List of evaluators to use for this source. See <see cref="EvaluatorKey"/> for valid values.
        /// </summary>
        public List<EvaluatorKey> Evaluators { get; set; }
    }
}
