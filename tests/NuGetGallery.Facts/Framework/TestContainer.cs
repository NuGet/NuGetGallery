using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Ninject;
using Ninject.Modules;

namespace NuGetGallery.Framework
{
    public class TestContainer : IDisposable
    {
        public IKernel Kernel { get; private set; }

        protected TestContainer()
        {
            // Initialize the container
            Kernel = UnitTestBindings.CreateContainer();
        }

        protected TController GetController<TController>() where TController : Controller
        {
            var c = Kernel.Get<TController>();
            c.ControllerContext = new ControllerContext(
                new RequestContext(Kernel.Get<HttpContextBase>(), new RouteData()), c);
            
            var routeCollection = new RouteCollection();
            Routes.RegisterRoutes(routeCollection);
            c.Url = new UrlHelper(c.ControllerContext.RequestContext, routeCollection);
            
            return c;
        }

        protected T Get<T>()
        {
            if(typeof(Controller).IsAssignableFrom(typeof(T))) {
                throw new InvalidOperationException("Use GetController<T> to get a controller instance");
            }
            return Kernel.Get<T>();
        }

        protected Mock<T> GetMock<T>() where T : class
        {
            T instance = Kernel.Get<T>();
            return Mock.Get(instance);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Kernel != null)
            {
                Kernel.Dispose();
                Kernel = null;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
        }
    }
}

