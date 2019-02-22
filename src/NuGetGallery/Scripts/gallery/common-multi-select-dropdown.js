function MultiSelectDropdownItem(value, text, name, checked, isRed) {
    this.value = value;
    this.text = text;
    this.name = name;
    this.checked = checked;
    this.isRed = isRed;
}

function MultiSelectDropdown(items, singularItemTitle, pluralItemTitle) {
    var self = this;

    this.dropdownSelector = '.multi-select-dropdown';
    this.dropdownBtnSelector = self.dropdownSelector + ' .dropdown-btn';

    this.dropdownOpen = ko.observable(false);
    this.toggleDropdown = function () {
        self.dropdownOpen(!self.dropdownOpen());
    };

    this.isAncestor = function (element, ancestorSelector) {
        var $target = $(element);
        // '.closest' returns the list of ancestors between this element and the selector.
        // If the selector is not an ancestor of the element, it returns an empty list.
        return $target.closest(ancestorSelector).length;
    };

    this.isElementInsideDropdown = function (element) {
        return self.isAncestor(element, self.dropdownSelector);
    };

    // If the user clicks outside of the dropdown, close it.
    $(document).click(function (event) {
        if (!self.isElementInsideDropdown(event.target)) {
            self.dropdownOpen(false);
        }
    });

    // If an element outside of the dropdown gains focus, close it.
    $(document).focusin(function (event) {
        if (!self.isElementInsideDropdown(event.target)) {
            self.dropdownOpen(false);
        }
    });

    this.escapeKeyCode = 27;
    $(document).keydown(function (event) {
        var target = event.target;
        if (self.isElementInsideDropdown(target)) {
            // If we press escape while focus is inside the dropdown, close it
            if (event.which === self.escapeKeyCode) { // Escape key
                self.dropdownOpen(false);
                event.preventDefault();
                $(self.dropdownBtnSelector).focus();
            }
        }
    });

    // A filter to be applied to the items
    this.filter = ko.observable('');
    this.filterPlaceholder = "Search for " + pluralItemTitle;

    // The items displayed in the dropdown.
    this.items = items;
    // Add the "checked" and "visible" observable properties to the items.
    ko.utils.arrayForEach(self.items, function (item) {
        item.checked = ko.observable(item.checked);
        item.visible = ko.pureComputed(function () {
            return item.value.startsWith(self.filter());
        }, this);
    }, this);

    // The items selected in the UI.
    this.chosenItems = ko.pureComputed(function () {
        return ko.utils
            .arrayFilter(
                self.items,
                function (item) { return item.checked(); })
            .map(function (item) { return item.value; });
    }, this);

    // A string to display to the user describing what is selected
    this.toggleText = ko.pureComputed(function () {
        var chosenItems = self.chosenItems();
        if (chosenItems.length === 0) {
            return "No " + pluralItemTitle;
        }

        if (chosenItems.length === self.items.length) {
            return "All " + pluralItemTitle;
        }

        return chosenItems.join(', ');
    }, this);
    this.toggleLabel = ko.pureComputed(function () {
        var chosenItems = self.chosenItems();
        if (chosenItems.length === 0) {
            return "No " + pluralItemTitle + " selected";
        }

        if (chosenItems.length === self.items.length) {
            return "All " + pluralItemTitle + " selected";
        }

        var itemTitle = chosenItems.length > 1 ? pluralItemTitle : singularItemTitle;
        return "Selected " + itemTitle + " " + chosenItems.join(', ');
    }, this);

    this.selectAllText = ko.pureComputed(function () {
        var prefix;
        if (self.filter()) {
            prefix = "Select filtered ";
        } else {
            prefix = "Select all ";
        }

        return prefix + pluralItemTitle;
    }, this);

    // Whether or not the select all checkbox for the items is selected.
    this.selectAllChecked = ko.pureComputed(function () {
        return !ko.utils
            .arrayFirst(
                self.items,
                function (item) {
                    // If an item is visible in the UI and is not checked, select all must not be checked.
                    return item.visible() && !item.checked();
                });
    }, this);

    // Toggles whether or not all visible items are selected.
    // If the checkbox is not selected, it selects all visible items.
    // If the checkbox is already selected, it deselects all visible items.
    this.toggleSelectAll = function () {
        var checked = !self.selectAllChecked();
        ko.utils.arrayForEach(
            self.items,
            function (item) {
                if (item.visible()) {
                    item.checked(checked);
                }
            });

        return true;
    };
}