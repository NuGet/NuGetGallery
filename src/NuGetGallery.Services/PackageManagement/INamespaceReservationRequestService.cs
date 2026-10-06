// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface INamespaceReservationRequestService
    {
        IReadOnlyList<NamespaceReservationRequest> GetRequestsForUser(User user);

        // Validate and save before assessing. Only form fields go to the model; database work stays here.
        // A saved approval triggers host-authorized allocation; the request retains the assessment decision.
        // Validation failures do not write data or call the model. An empty Errors collection means saved.
        Task<NamespaceReservationSubmissionResult> SubmitAsync(User submitter, NamespaceReservationRequestInput input);
    }
}