using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Web;
using System.Text.RegularExpressions;
using System.Web;

namespace NuGetGallery
{
    // Based on http://blog.maartenballiauw.be/post/2011/11/08/Rewriting-WCF-OData-Services-base-URL-with-load-balancing-reverse-proxy.aspx
    public sealed class RewriteBaseUrlMessageInspector : Attribute, IServiceBehavior, IDispatchMessageInspector
    {
        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            var context = WebOperationContext.Current;
            if (HttpContext.Current != null && context != null && context.IncomingRequest.UriTemplateMatch != null)
            {
                var curatedFeedName = HttpContext.Current.Request.QueryString["name"];
                
                // Grab the base and request URIs
                UriBuilder baseUriBuilder = new UriBuilder(context.IncomingRequest.UriTemplateMatch.BaseUri);
                UriBuilder requestUriBuilder = new UriBuilder(context.IncomingRequest.UriTemplateMatch.RequestUri);

                // Replace Host Name
                baseUriBuilder.Host = HttpContext.Current.Request.Url.Host;
                requestUriBuilder.Host = baseUriBuilder.Host;

                // Replace "/api/v2/curated-feed" with "/api/v2/curated-feeds/[feedname]"
                baseUriBuilder.Path = RewriteUrlPath(baseUriBuilder.Path, curatedFeedName);
                requestUriBuilder.Path = RewriteUrlPath(requestUriBuilder.Path, curatedFeedName);

                // Set the matching properties on the incoming request
                OperationContext.Current.IncomingMessageProperties["MicrosoftDataServicesRootUri"] = baseUriBuilder.Uri;
                OperationContext.Current.IncomingMessageProperties["MicrosoftDataServicesRequestUri"] = requestUriBuilder.Uri;
            }

            return null;
        }

        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            foreach (ChannelDispatcher channel in serviceHostBase.ChannelDispatchers)
            {
                foreach (var endpoint in channel.Endpoints)
                {
                    // Attach ourselves to all endpoints of all channels
                    endpoint.DispatchRuntime.MessageInspectors.Add(new RewriteBaseUrlMessageInspector());
                }
            }
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            // No-op
        }

        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, System.Collections.ObjectModel.Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
        {
            // No-op
        }

        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            // No-op
        }

        private static Regex UrlPattern = new Regex("/api/v2/curated-feed(/)?");
        private static string ReplacementFormat = "/api/v2/curated-feeds/{0}$1";
        public static string RewriteUrlPath(string path, string curatedFeedName)
        {
            return UrlPattern.Replace(path, String.Format(CultureInfo.InvariantCulture, ReplacementFormat, curatedFeedName));
        }
    }
}