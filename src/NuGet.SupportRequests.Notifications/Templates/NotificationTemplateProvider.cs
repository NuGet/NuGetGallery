// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace NuGet.SupportRequests.Notifications.Templates
{
    internal static class NotificationTemplateProvider
    {
        private static readonly string _cssStylesResourceName;
        private static readonly string _templatesNamespace;
        private static readonly Assembly _templateAssembly;
        private static readonly IDictionary<string, string> _templateCache;

        static NotificationTemplateProvider()
        {
            var type = typeof(NotificationTemplateProvider);

            _templatesNamespace = type.Namespace;
            _cssStylesResourceName = $"{_templatesNamespace}.EmailStyles.css";
            _templateAssembly = type.Assembly;
            _templateCache = new Dictionary<string, string>();
        }

        internal static string Get(string name)
        {
            string template;
            if (_templateCache.ContainsKey(name))
            {
                template = _templateCache[name];
            }
            else
            {
                var htmlTemplate = ReadTemplateFromResourceName($"{_templatesNamespace}.{name}");
                var cssStyles = ReadTemplateFromResourceName(_cssStylesResourceName);

                // apply CSS styles
                template = htmlTemplate
                    .Replace(HtmlPlaceholders.CssStyles, cssStyles);

                _templateCache[name] = template;
            }

            return template;
        }

        private static string ReadTemplateFromResourceName(string resourceName)
        {
            string template;
            using (var stream = _templateAssembly.GetManifestResourceStream(resourceName))
            {
                using (var reader = new StreamReader(stream))
                {
                    template = reader.ReadToEnd();
                }
            }

            return template;
        }
    }
}