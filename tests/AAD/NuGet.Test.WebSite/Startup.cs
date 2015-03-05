using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(NuGet.Test.WebSite.Startup))]
namespace NuGet.Test.WebSite
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
