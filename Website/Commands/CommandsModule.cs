using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ninject.Modules;

namespace NuGetGallery.Commands
{
    public class CommandsModule : NinjectModule
    {
        public override void Load()
        {
            // Bind command executor
            Bind<ICommandExecutor>()
                .To<CommandExecutor>()
                .InSingletonScope();

            // Bind handlers
            var types = from t in typeof(CommandsModule).Assembly.GetExportedTypes()
                        where t.IsClass && !t.IsAbstract
                        let b = FindHandlerType(t)
                        where b != null
                        select new { Type = t, BaseType = b };
            foreach (var type in types)
            {
                Bind(type.BaseType).To(type.Type).InRequestScope();
            }
        }

        private Type FindHandlerType(Type type)
        {
            while (type != null && (!type.IsGenericType || (type.GetGenericTypeDefinition() != typeof(CommandHandler<,>))))
            {
                type = type.BaseType;
            }
            return type;
        }
    }
}
