using Ninject.Modules;
using System.Data.Entity;

namespace NuGetGallery 
{
    public class ContainerBindings : NinjectModule 
    {
        public override void Load() 
        {
            Bind<IConfiguration>()
                .To<Configuration>()
                .InSingletonScope();

            Bind<EntitiesContext>()
                .ToMethod(context => new EntitiesContext())
                .InRequestScope();
            
            Bind<IEntityRepository<User>>()
                .To<EntityRepository<User>>()
                .InRequestScope();

            Bind<IEntityRepository<PackageRegistration>>()
                .To<EntityRepository<PackageRegistration>>()
                .InRequestScope();

            Bind<IEntityRepository<Package>>()
                .To<EntityRepository<Package>>()
                .InRequestScope();

            Bind<IUsersService>()
                .To<UsersService>()
                .InRequestScope();

            Bind<IPackageService>()
                .To<PackageService>()
                .InRequestScope();
            
            Bind<IPackageFileService>()
                .To<FileSystemPackageFileService>()
                .InRequestScope();

            Bind<ICryptographyService>()
                .To<CryptographyService>()
                .InRequestScope();

            Bind<IFormsAuthenticationService>()
                .To<FormsAuthenticationService>()
                .InSingletonScope();
        }
    }
}
