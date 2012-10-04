using System;
using System.Linq;
using System.Collections.Generic;

namespace NuGetGallery.ViewModels.PackagePart
{
    public class PackageItem : IComparable<PackageItem>    
    {
        private readonly string _name;
        private readonly PackageItem _parent;
        private readonly bool _isFile;

        public PackageItem(string name)
            : this(name, parent: null, isFile: false)
        {
        }

        public PackageItem(string name, PackageItem parent)
            : this(name, parent, isFile: false)
        {
        }

        public PackageItem(string name, PackageItem parent, bool isFile)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }

            _name = name;

            // parent can be null (e.g. for the root node)
            _parent = parent;

            _isFile = isFile;

            Children = new SortedSet<PackageItem>();
        }

        public PackageItem Parent
        {
            get { return _parent; }
        }

        public string Name
        {
            get { return _name; }
        }

        public bool IsFile
        {
            get { return _isFile; }
        }

        public ICollection<PackageItem> Children { get; private set; }

        public PackageItem this[string name]
        {
            get { return Children.SingleOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); }
        }

        public int CompareTo(PackageItem other)
        {
            if (this == other)
            {  
                return 0;
            }

            if (other == null)
            {
                return 1;
            }

            // folder goes before file
            if (!this.IsFile && other.IsFile)
            {
                return -1;
            }

            if (this.IsFile && !other.IsFile)
            {
                return 1;
            }

            return String.Compare(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            var other = obj as PackageItem;
            if (other == null)
            {
                return false;
            }

            return CompareTo(other) == 0;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}