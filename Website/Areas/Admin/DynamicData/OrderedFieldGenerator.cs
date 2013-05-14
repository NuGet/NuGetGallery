using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.DynamicData;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public class OrderedFieldGenerator : IAutoFieldGenerator
    {
        private static readonly Dictionary<string, string[]> SortOrders = new Dictionary<string, string[]>()
        {
            {"Users", new [] { "Username", "EmailAddress", "UnconfirmedEmailAddress" } },
            {"CuratedPackages", new [] { "PackageRegistration", "CuratedFeed", "Notes" } },
            {"EmailMessages", new [] { "Subject", "Body", "FromUser", "ToUser", "Sent" } },
            {"PackageDependencies", new [] { "Package", "Id", "VersionSpec", "TargetFramework" } },
            {"PackageFrameworks", new [] { "Package" } },
            {"PackageOwnerRequests", new [] { "PackageRegistrationKey", "NewOwner", "RequestingOwner", "RequestDate", "ConfirmationCode" } },
            {"PackageRegistrations", new [] { "Id", "Owners", "Packages", "DownloadCount" } },
            {"Packages", new [] { "PackageRegistration", "Title", "Version", "Created", "Description" } },
            {"PackageStatistics", new [] { "Package", "Operation", "UserAgent", "IPAddress" } }
        };

        protected MetaTable _table;

        public OrderedFieldGenerator(MetaTable table)
        {
            _table = table;
        }

        public ICollection GenerateFields(Control control)
        {
            var itemControl = control as IDataBoundItemControl;
            var containerType = 
                ((!(control is IDataBoundListControl) && !(control is Repeater)) && (control is IDataBoundItemControl)) ?
                ContainerType.Item :
                ContainerType.List;
            var mode = itemControl == null ? DataBoundControlMode.ReadOnly : itemControl.Mode;

            var columns = _table.GetScaffoldColumns(mode, containerType);
            
            return SortColumns(_table, columns)
                .Select(col => {
                    var field = new DynamicField()
                    {
                        DataField = col.Name,
                        HeaderText = (containerType == ContainerType.List) ? col.ShortDisplayName : col.Name
                    };
                    if (containerType != ContainerType.List)
                    {
                        field.ItemStyle.BorderWidth = 0;
                    }
                    field.ItemStyle.Wrap = false;
                    return field;
                })
                .ToList();
        }

        private static IEnumerable<MetaColumn> SortColumns(MetaTable table, IEnumerable<MetaColumn> columns)
        {
            string[] order;
            if (!SortOrders.TryGetValue(table.Name, out order))
            {
                order = new string[0];
            }
            return Enumerable.Concat(
                order.Select(s => columns.Single(c => String.Equals(c.Name, s))),
                columns.Where(c => !order.Contains(c.Name)));
        }
    }
}