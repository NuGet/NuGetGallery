// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.AccountDeleter
{
    public class AccountDeleteConfiguration
    {
        /// <summary>
        /// Indicates if a users' AllowEmailContact setting is respected by the instance.
        /// </summary>
        public bool RespectEmailContactSetting { get; set; }

        /// <summary>
        /// List of configurations indicating possible sources of message and how they should be handled.
        /// </summary>
        public List<SourceConfiguration> SourceConfigurations { get; set; }

        /// <summary>
        /// Nested configurations for sending email messages.
        /// </summary>
        public EmailConfiguration EmailConfiguration { get; set; }

        /// <summary>
        /// Default template replacements list.
        /// </summary>
        public Dictionary<string, string> TemplateReplacements { get; set; }

        /// <summary>
        /// Storage container connection string where gallery content can be found
        /// </summary>
        public string GalleryStorageConnectionString { get; set; }

        /// <summary>
        /// This method get the configuration for a source.
        /// </summary>
        /// <param name="source"></param>
        /// <exception cref="UnknownSourceException">Throws <see cref="UnknownSourceException"/> when source requested is not present in configuration.</exception>
        public SourceConfiguration GetSourceConfiguration(string source)
        {
            foreach (var sourceConfig in SourceConfigurations)
            {
                if (sourceConfig.SourceName == source)
                {
                    return sourceConfig;
                }
            }

            throw new UnknownSourceException();
        }
    }
}