using System;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using AnglicanGeek.MarkdownMailer;
using Ninject.Modules;

namespace NuGetGallery {
    public class ContainerBindings : NinjectModule {
        public override void Load() {
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

            Bind<IEntityRepository<PackageAuthor>>()
                .To<EntityRepository<PackageAuthor>>()
                .InRequestScope();

            Bind<IEntityRepository<PackageDependency>>()
                .To<EntityRepository<PackageDependency>>()
                .InRequestScope();

            Bind<IUserService>()
                .To<UserService>()
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

            Bind<IFileSystemService>()
                .To<FileSystemService>()
                .InSingletonScope();

            Bind<IControllerFactory>()
                .To<NuGetControllerFactory>()
                .InRequestScope();

            Lazy<IMailSender> mailSenderThunk = new Lazy<IMailSender>(() => {
                var mailSenderConfiguration = new MailSenderConfiguration() {
                    DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                    PickupDirectoryLocation = HttpContext.Current.Server.MapPath("~/App_Data/Mail")
                };

                return new MailSender(mailSenderConfiguration);
            });

            Bind<IMailSender>()
                .ToMethod(context => mailSenderThunk.Value);

            Bind<IMessageService>()
                .To<MessageService>();
        }
    }
}
