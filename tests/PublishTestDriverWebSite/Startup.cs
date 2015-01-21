using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(PublishTestDriverWebSite.Startup))]
namespace PublishTestDriverWebSite
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
