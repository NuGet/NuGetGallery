using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace NuCmd
{
    public class CommandDefinition
    {
        private static readonly Regex NameParser = new Regex(@"^NuCmd\.Commands\.((?<group>[^\.]*)\.)?(?<command>[^\.]*)Command$", RegexOptions.IgnoreCase);
        
        public string Group { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public Type Type { get; private set; }

        public CommandDefinition(string group, string name, string description, Type type)
        {
            Group = group;
            Name = name;
            Description = description;
            Type = type;
        }

        public static CommandDefinition FromType(Type type)
        {
            var descAttr = type.GetCustomAttribute<DescriptionAttribute>();
            var match = NameParser.Match(type.FullName);

            if (!match.Success)
            {
                return null;
            }
            else
            {
                string name = match.Groups["command"].Value.ToLowerInvariant();
                string group = null;
                if (match.Groups["group"].Success)
                {
                    group = match.Groups["group"].Value.ToLowerInvariant();
                }

                return new CommandDefinition(group, name, descAttr == null ? null : descAttr.Description, type);
            }
        }

        public static IList<CommandDefinition> GetAllCommands()
        {
            return typeof(Program)
                .Assembly
                .GetExportedTypes()
                .Where(t => !t.IsAbstract && t.Namespace.StartsWith("NuCmd.Commands"))
                .Select(CommandDefinition.FromType)
                .ToList();
        }
    }
}
