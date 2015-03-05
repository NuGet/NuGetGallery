using Microsoft.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGet.Services.Publish
{
    public class PublicationVisibility
    {
        public enum VisibilityScope { Public, Organization, Subscription };

        public VisibilityScope Visibility { get; private set; }

        public string Organization { get; private set; }

        public string Subscription { get; private set; }

        public static bool TryCreate(IOwinContext context, out PublicationVisibility visibility)
        {
            string subscription = context.Request.Query["subscription"];

            string organization = context.Request.Query["organization"];

            if (subscription == null && organization == null)
            {
                visibility = new PublicationVisibility { Visibility = VisibilityScope.Public };
                return true;
            }
            if (subscription != null && organization != null)
            {
                visibility = null;
                return false;
            }
            if (subscription != null)
            {
                visibility = new PublicationVisibility { Visibility = VisibilityScope.Subscription, Subscription = subscription };
                return true;
            }
            if (organization != null)
            {
                visibility = new PublicationVisibility { Visibility = VisibilityScope.Organization, Organization = organization };
                return true;
            }

            visibility = null;
            return false;
        }
    }
}