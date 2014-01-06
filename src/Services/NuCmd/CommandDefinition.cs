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

            string name;
            string group;
            if (!match.Success)
            {
                name = type.Name;
                group = null;
            }
            else
            {
                name = match.Groups["command"].Value.ToLowerInvariant();
                if (match.Groups["group"].Success)
                {
                    group = match.Groups["group"].Value.ToLowerInvariant();
                }
                else
                {
                    group = null;
                }
            }

            return new CommandDefinition(group, name, descAttr == null ? null : descAttr.Description, type);
        }
    }
}
