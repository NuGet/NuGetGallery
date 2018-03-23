// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public class SignatureValidatorResult
    {
        public SignatureValidatorResult(ValidationStatus state, Uri nupkgUri) 
            : this(state, new IValidationIssue[0], nupkgUri)
        {
        }

        public SignatureValidatorResult(ValidationStatus state, IReadOnlyList<IValidationIssue> issues, Uri nupkgUri)
        {
            if (state != ValidationStatus.Failed
                && state != ValidationStatus.Succeeded
                && issues.Any())
            {
                throw new ArgumentException("Issues are only allowed for terminal states.", nameof(issues));
            }

            if (state != ValidationStatus.Succeeded
                && nupkgUri != null)
            {
                throw new ArgumentException("A .nupkg URI is only allowed for a successful result.", nameof(nupkgUri));
            }

            State = state;
            Issues = issues ?? throw new ArgumentNullException(nameof(issues));
            NupkgUri = nupkgUri;
        }

        public ValidationStatus State { get; }
        public IReadOnlyList<IValidationIssue> Issues { get; }
        public Uri NupkgUri { get; }
    }
}