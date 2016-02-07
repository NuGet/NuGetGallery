using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Services
{
    public enum SupportRequestDeleteAdminResult
    {
          EmptyUserName,
          AdminNotPresent,
          AdminHasAssignedIssues,
          DeleteSuccessful

    }
}