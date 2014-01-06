using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NuCmd.Commands;

namespace NuCmd
{
    public class CommandDirectory
    {
        private static readonly IReadOnlyDictionary<string, CommandDefinition> EmptyCommands = 
            new ReadOnlyDictionary<string, CommandDefinition>(new Dictionary<string, CommandDefinition>());
        private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, CommandDefinition>> EmptyGroups = 
            new ReadOnlyDictionary<string, IReadOnlyDictionary<string, CommandDefinition>>(
                new Dictionary<string, IReadOnlyDictionary<string, CommandDefinition>>());

        private List<CommandDefinition> _commands = new List<CommandDefinition>();
        private IReadOnlyDictionary<string, IReadOnlyDictionary<string, CommandDefinition>> _groups = EmptyGroups;
        private IReadOnlyDictionary<string, CommandDefinition> _rootCommands = EmptyCommands;
            

        public IReadOnlyDictionary<string, CommandDefinition> RootCommands { get { return _rootCommands; } }
        public IReadOnlyList<CommandDefinition> Commands { get { return _commands.AsReadOnly(); } }
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, CommandDefinition>> Groups { get { return _groups; } }

        public CommandDirectory()
        {
        }

        public void LoadCommands(params Assembly[] assemblies)
        {
            _commands = assemblies
                .SelectMany(a => 
                    a.GetExportedTypes()
                     .Where(t => !t.IsAbstract && t.Namespace.StartsWith("NuCmd.Commands") && typeof(ICommand).IsAssignableFrom(t))
                     .Select(CommandDefinition.FromType))
                .ToList();
            _groups = new ReadOnlyDictionary<string, IReadOnlyDictionary<string, CommandDefinition>>(
                _commands
                    .GroupBy(c => c.Group ?? String.Empty)
                    .Where(c => !String.IsNullOrEmpty(c.Key))
                    .ToDictionary(
                        g => g.Key,
                        g => (IReadOnlyDictionary<string, CommandDefinition>)new ReadOnlyDictionary<string, CommandDefinition>(
                            g.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase)),
                        StringComparer.OrdinalIgnoreCase));
            _rootCommands = _commands
                .Where(c => String.IsNullOrEmpty(c.Group))
                .ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyDictionary<string, CommandDefinition> GetGroup(string group)
        {
            if (String.IsNullOrEmpty(group))
            {
                return RootCommands;
            }

            IReadOnlyDictionary<string, CommandDefinition> commands;
            if (!Groups.TryGetValue(group, out commands))
            {
                return EmptyCommands;
            }
            return commands;
        }

        public CommandDefinition GetCommand(string group, string name)
        {
            CommandDefinition command;
            if (!GetGroup(group).TryGetValue(name, out command))
            {
                return null;
            }
            return command;
        }
    }
}
