// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NuGetGallery.Operations.Tasks
{
    [Command("settings", "Show deployment settings received from the Ops console", IsSpecialPurpose = true)]
    public class ListDeploymentSettingsTask : OpsTask
    {
        private static readonly Dictionary<SettingType, string> _fullNames = new Dictionary<SettingType, string>() {
            { SettingType.Db, "Main Database Connection String" },
            { SettingType.Warehouse, "Warehouse Database Connection String" },
            { SettingType.Storage, "Azure Storage Connection String" },
            { SettingType.Backup, "Backup Azure Storage Connection String" }
        };

        private static readonly Dictionary<SettingType, Func<DeploymentEnvironment, string>> _fetcher = new Dictionary<SettingType, Func<DeploymentEnvironment, string>>() {
            { SettingType.Db, e => e.MainDatabase.ConnectionString },
            { SettingType.Warehouse, e => e.WarehouseDatabase.ConnectionString },
            { SettingType.Storage, e => e.MainStorage.ToString(exportSecrets: true) },
            { SettingType.Backup, e => e.BackupStorage.ToString(exportSecrets: true) }
        };

        [Option("Show all settings")]
        public bool All { get; set; }

        [Option("Specify this argument to place one of the specified Connection Strings (Db, Warehouse, Storage, Backup) in the clipboard")]
        public string Clip { get; set; }

        public enum SettingType
        {
            Db,
            Warehouse,
            Storage,
            Backup
        }

        public override void ExecuteCommand()
        {
            if (CurrentEnvironment == null || String.IsNullOrEmpty(CurrentEnvironment.EnvironmentName))
            {
                Log.Warn("No current environment!");
            }
            else if (!String.IsNullOrEmpty(Clip))
            {
                // Parse the value
                var values = Enum.GetValues(typeof(SettingType))
                    .Cast<SettingType>()
                    .Where(st => st.ToString().StartsWith(Clip, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (values.Count == 0)
                {
                    Log.Error("Unknown setting type '{0}'", Clip);
                } else if (values.Count > 1) {
                    Log.Error("Ambiguous setting type '{0}'. Matches: {1}", Clip, String.Join(", ", values.Select(s=> "'" + s.ToString() + "'")));
                } else {
                    var value = values[0];
                    Log.Info("Placing {0} in clipboard", _fullNames[value]);

                    Thread t = new Thread(() => Clipboard.SetText(_fetcher[value](CurrentEnvironment), TextDataFormat.UnicodeText));
                    t.SetApartmentState(ApartmentState.STA);
                    t.Start();
                    t.Join();
                }
            }
            else if (!All)
            {
                Log.Info("Environment: {0}", EnvironmentName);
                Log.Info(" Subscription: {0} ({1})", CurrentEnvironment.SubscriptionName, CurrentEnvironment.SubscriptionId);
                Log.Info(" Main SQL: {0}", CurrentEnvironment.MainDatabase == null ? "<unknown>" : CurrentEnvironment.MainDatabase.DataSource);
                Log.Info(" Warehouse SQL: {0}", CurrentEnvironment.WarehouseDatabase == null ? "<unknown>" : CurrentEnvironment.WarehouseDatabase.DataSource);
                Log.Info(" Main Storage: {0}", CurrentEnvironment.MainStorage == null ? "<unknown>" : CurrentEnvironment.MainStorage.Credentials.AccountName);
                Log.Info(" Backup Storage: {0}", CurrentEnvironment.BackupStorage == null ? "<unknown>" : CurrentEnvironment.BackupStorage.Credentials.AccountName);
                Log.Info(" SQL DAC: {0}", CurrentEnvironment.SqlDacEndpoint == null ? "<unknown>" : CurrentEnvironment.SqlDacEndpoint.AbsoluteUri);
            }
            else
            {
                Log.Info("All settings for {0}", EnvironmentName);
                foreach (var pair in CurrentEnvironment.Settings)
                {
                    Log.Info("* {0} = {1}", pair.Key, pair.Value);
                }
            }
        }
    }
}
