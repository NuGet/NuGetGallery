// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class ApiKeyViewModel
    {
        public ApiKeyViewModel()
        {
        }

        public ApiKeyViewModel(CredentialViewModel cred)
        {
            if (cred == null)
            {
                throw new ArgumentNullException(nameof(cred));
            }

            var scopes = cred
                .Scopes
                .Select(s => s.AllowedAction)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            var subjects = cred
                .Scopes
                .Select(s => s.Subject)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            var globPattern = subjects
                .FirstOrDefault(s => s != null && s.Contains("*"));

            var packages = subjects
                .Except(new[] { globPattern })
                .ToList();

            Key = cred.Key;
            Type = cred.Type;
            Value = cred.Value;
            Description = cred.Description;
            Expires = cred.Expires?.ToString("O");
            HasExpired = cred.HasExpired;
            IsNonScopedV1ApiKey = cred.IsNonScopedV1ApiKey;
            Scopes = scopes;
            Packages = packages;
            GlobPattern = globPattern;
        }

        public bool IsNonScopedV1ApiKey { get; set; }
        public bool HasExpired { get; set; }
        public string Expires { get; set; }
        public IList<string> Scopes { get; set; }
        public IList<string> Packages { get; set; }
        public string GlobPattern { get; set; }
        public string Value { get; set; }
        public string Type { get; set; }
        public int Key { get; set; }
        public string Description { get; set; }
    }
}