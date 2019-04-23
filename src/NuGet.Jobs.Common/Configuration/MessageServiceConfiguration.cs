// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Mail;
using NuGet.Services.Messaging.Email;

namespace NuGet.Jobs.Configuration
{
    public class MessageServiceConfiguration : IMessageServiceConfiguration
    {
        private static readonly string DefaultMailAddress = "NuGet Gallery <support@nuget.org>";

        public string GalleryOwner { get; set; } = DefaultMailAddress;

        public string GalleryNoReplyAddress { get; set; } = DefaultMailAddress;

        MailAddress IMessageServiceConfiguration.GalleryOwner
        {
            get => StringToMailAddress(GalleryOwner);
            set => GalleryOwner = MailAddressToString(value);
        }

        MailAddress IMessageServiceConfiguration.GalleryNoReplyAddress
        {
            get => StringToMailAddress(GalleryNoReplyAddress);
            set => GalleryNoReplyAddress = MailAddressToString(value);
        }

        private static MailAddress StringToMailAddress(string input)
        {
            if (input == null)
            {
                return null;
            }

            return new MailAddress(input);
        }

        private static string MailAddressToString(MailAddress mailAddress)
        {
            if (mailAddress == null)
            {
                return null;
            }

            return $"{mailAddress.DisplayName} <{mailAddress.Address}>";
        }
    }
}
