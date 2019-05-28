// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net.Mail;
using System.Threading.Tasks;
using AnglicanGeek.MarkdownMailer;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Configuration;
using NuGetGallery.Services.Telemetry;

namespace NuGetGallery.Infrastructure.Mail
{
    public class MarkdownMessageService : CoreMarkdownMessageService, IMessageService
    {
        private readonly ITelemetryService _telemetryService;
        private readonly string _smtpUri;

        public MarkdownMessageService(
            IMailSender mailSender,
            IAppConfiguration config,
            ITelemetryService telemetryService)
            : base(mailSender, config)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _smtpUri = config.SmtpUri?.Host;
        }

        protected override async Task AttemptSendMessageAsync(MailMessage mailMessage, int attemptNumber)
        {
            var success = false;
            var startTime = DateTimeOffset.UtcNow;
            var sw = Stopwatch.StartNew();
            try
            {
                await base.AttemptSendMessageAsync(mailMessage, attemptNumber);
                success = true;
            }
            finally
            {
                sw.Stop();
                _telemetryService.TrackSendEmail(_smtpUri, startTime, sw.Elapsed, success, attemptNumber);
            }
        }
    }
}