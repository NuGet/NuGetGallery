using System;

namespace NuGetGallery.Operations.Common
{
    public static class ArgCheck
    {
        public static void RequiredOrConfig(object value, string name)
        {
            if (value == null)
            {
                throw CreateRequiredOrConfigEx(name);
            }
        }

        public static void RequiredOrConfig(string value, string name)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                throw CreateRequiredOrConfigEx(name);
            }
        }

        public static void Required(object value, string name)
        {
            if (value == null)
            {
                throw CreateRequiredEx(name);
            }
        }

        public static void Required(string value, string name)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                throw CreateRequiredEx(name);
            }
        }

        private static CommandLineException CreateRequiredEx(string name)
        {
            return new CommandLineException(String.Format(CommandHelp.Option_Required, name));
        }

        private static CommandLineException CreateRequiredOrConfigEx(string name)
        {
            return new CommandLineException(String.Format(CommandHelp.Option_RequiredOrConfig, name));
        }
    }
}
