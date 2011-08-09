using Ninject.Modules;

namespace NuGet.Server.Infrastructure {
    public class Bindings : NinjectModule {
        public override void Load() {
            IServerPackageRepository packageRepository = new ServerPackageRepository(PackageUtility.PackagePhysicalPath);
            Bind<IHashProvider>().To<CryptoHashProvider>();
            Bind<IServerPackageRepository>().ToConstant(packageRepository);
            Bind<PackageService>().ToSelf();
            Bind<IPackageAuthenticationService>().To<PackageAuthenticationService>();
        }
    }
}
