'use strict';

function ManageDeprecationViewModel(id, versionDeprecationStateDictionary, defaultVersion, submitUrl, packageUrl, getAlternatePackageVersionsUrl) {
    var self = this;

    // Existing deprecation state information per version.
    var items = Object.keys(versionDeprecationStateDictionary).map(function (version) {
        var versionData = versionDeprecationStateDictionary[version];
        return new MultiSelectDropdownItem(
            version,
            versionData.Text,
            version,
            version === defaultVersion,
            versionData.IsLegacy || versionData.IsOther);
    });

    this.dropdown = new MultiSelectDropdown(items, "version", "versions");
    this.chosenItemsConflictWarning = ko.pureComputed(function () {
        var chosenItems = self.dropdown.chosenItems();
        var isLegacy = self.isLegacy();
        var isOther = self.isOther();
        var warningMessage = null;
        var areMultipleVersionsSelected = chosenItems.length > 1;

        if (chosenItems.length === 0) {
            // If nothing is selected, an error will show
            // No need to show a warning in addition to the error
            return null;
        }

        var hasVersionsWithExistingDeprecationState = false;
        for (var i in chosenItems) {
            var version = chosenItems[i];
            var versionData = versionDeprecationStateDictionary[version];
            if (!versionData) {
                // It shouldn't be possible to select a version that didn't exist when the page loaded.
                // In case there is a bug and the user did select a valid version, continue on anyway.
                continue;
            }

            if (versionData.IsLegacy || versionData.IsOther) {
                hasVersionsWithExistingDeprecationState = true;
                break;
            }
        }

        if (isLegacy || isOther) {
            if (areMultipleVersionsSelected && hasVersionsWithExistingDeprecationState) {
                // Show a warning if multiple versions are selected and at least one has an existing deprecation
                // The user should be aware they are replacing existing deprecations
                // Don't show an alert if a single version is selected because it is clear that the deprecation is being replaced
                warningMessage = "Some of the package versions you selected have already been deprecated. All selected versions will have their deprecation information overridden or removed when you submit this form.";
            }
        } else if (hasVersionsWithExistingDeprecationState) {
            // Show a warning if no reasons are selected and at least one selected version has an existing deprecation
            // The user should be aware that they are deleting the existing deprecation
            warningMessage = "The version" + (areMultipleVersionsSelected ? "s" : "") +
                " you selected will have " + (areMultipleVersionsSelected ? "their" : "its") +
                " deprecation information removed.";
        }

        return warningMessage;
    }, this);

    this.isLegacy = ko.observable(false);
    this.isOther = ko.observable(false);

    // The ID entered into the alternate package ID textbox.
    this.chosenAlternatePackageId = ko.observable('');

    // The version chosen by the alternate package version dropdown.
    this.chosenAlternatePackageVersion = ko.observable();

    // The cached list of versions associated with the currently entered alternate package ID.
    this.alternatePackageVersionsCached = ko.observableArray();

    // The list of options in the alternate package version dropdown.
    this.alternatePackageVersions = ko.pureComputed(function () {
        // Include an "Any Version" option in case users want to select the package registration.
        return [strings_AnyVersion].concat(self.alternatePackageVersionsCached());
    }, this);

    // Whether or not the versions of the currently entered alternate package ID have been loaded.
    this.hasAlternatePackageVersions = ko.pureComputed(function () {
        return self.alternatePackageVersionsCached().length > 0;
    }, this);

    // The error to show with the currently entered alternate package ID.
    // E.g. the package does not exist or cannot be chosen as an alternate.
    this.chosenAlternatePackageIdError = ko.observable();

    // When a new alternate package ID is entered, load the list of versions from the server.
    this.chosenAlternatePackageId.subscribe(function (id) {
        if (!id) {
            // If the user hasn't input an ID, don't query the server.
            self.chosenAlternatePackageIdError(null);
            return;
        }

        $.ajax({
            url: getAlternatePackageVersionsUrl,
            dataType: 'json',
            type: 'GET',
            data: {
                id: id
            },

            statusCode: {
                200: function (data) {
                    if (self.alternatePackageId() === id) {
                        if (data.length) {
                            self.alternatePackageVersionsCached(data);
                            self.chosenAlternatePackageIdError(null);
                        } else {
                            self.alternatePackageVersionsCached.removeAll();
                            self.chosenAlternatePackageIdError("Could not find alternate package '" + id + "'.");
                        }
                    }
                }
            },

            error: function () {
                if (self.alternatePackageId() === id) {
                    self.alternatePackageVersionsCached.removeAll();
                    self.chosenAlternatePackageIdError("An unknown occurred when searching for alternate package '" + id + "'.");
                }
            }
        });
    }, this);

    // The alternate package ID to submit with the form.
    this.alternatePackageId = ko.pureComputed(function () {
        if (self.isLegacy()) {
            return self.chosenAlternatePackageId();
        } else {
            // If the legacy checkbox is not selected, this section of the form is hidden.
            // Don't submit the chosen alternate package ID with the form.
            return null;
        }
    }, this);
    this.alternatePackageVersion = ko.pureComputed(function () {
        if (self.alternatePackageId()) {
            var version = self.chosenAlternatePackageVersion();
            // If the chosen version is the "Any Version" string, don't submit it with the form.
            if (version !== strings_AnyVersion) {
                return version;
            }
        }

        // If there is no alternate package ID to submit with the form, don't submit the chosen alternate package version.
        return null;
    }, this);

    // The custom message to submit with the form.
    this.customMessage = ko.observable('');

    this.submitError = ko.observable();
    this.submit = function () {
        self.submitError(null);

        var alertMessage = self.chosenItemsConflictWarning();
        if (alertMessage && !confirm(alertMessage + " Do you want to continue?")) {
            return;
        }

        $.ajax({
            url: submitUrl,
            dataType: 'json',
            type: 'POST',
            data: window.nuget.addAjaxAntiForgeryToken({
                id: id,
                versions: self.dropdown.chosenItems(),
                isLegacy: self.isLegacy(),
                isOther: self.isOther(),
                alternatePackageId: self.alternatePackageId(),
                alternatePackageVersion: self.alternatePackageVersion(),
                customMessage: self.customMessage()
            }),
            success: function () {
                window.location.href = packageUrl;
            },
            error: function (jqXHR) {
                var newError = jqXHR && jqXHR.responseJSON ? jqXHR.responseJSON.error : "An unknown error occurred when submitting the form.";
                self.submitError(newError);
            }
        });
    };

    // Clone the version deprecation state dictionary so that we can remember form state when the selected versions change.
    // The default state for a selected version is its current deprecation state.
    // Converting the existing dictionary to JSON and then parsing it is a very cheap way to do a deep copy.
    var versionDeprecationFormState = JSON.parse(JSON.stringify(versionDeprecationStateDictionary));

    var saveDeprecationFormState = function (version) {
        var versionData = versionDeprecationFormState[version];
        if (!versionData) {
            return;
        }

        versionData.IsLegacy = self.isLegacy();
        versionData.IsOther = self.isOther();
        versionData.AlternatePackageId = self.alternatePackageId();
        versionData.AlternatePackageVersion = self.alternatePackageVersion();
        versionData.CustomMessage = self.customMessage();
    };

    var loadDeprecationFormState = function (version) {
        var versionData = versionDeprecationFormState[version];
        if (!versionData) {
            return;
        }

        self.isLegacy(versionData.IsLegacy);
        self.isOther(versionData.IsOther);

        self.chosenAlternatePackageId(versionData.AlternatePackageId);
        if (versionData.AlternatePackageVersion) {
            self.alternatePackageVersionsCached([versionData.AlternatePackageVersion]);
            self.chosenAlternatePackageVersion(versionData.AlternatePackageVersion);
        }

        self.customMessage(versionData.CustomMessage);
    };

    // When the chosen versions are changed, remember the contents of the form in case the user navigates back to this version.
    this.dropdown.chosenItems.subscribe(function (oldVersions) {
        // Reset the error when the chosen items change.
        self.submitError(null);

        if (!oldVersions || oldVersions.length !== 1) {
            // If no versions or multiple versions are selected, don't cache the contents of the form.
            return;
        }

        saveDeprecationFormState(oldVersions[0]);
    }, this, "beforeChange");

    // When the chosen versions are changed, load the existing deprecation state for this version.
    this.dropdown.chosenItems.subscribe(function (newVersions) {
        if (!newVersions || newVersions.length !== 1) {
            // If no versions or multiple versions are selected, don't load the existing deprecation state.
            return;
        }

        loadDeprecationFormState(newVersions[0]);
    }, this);

    // Load the state for the default version.
    loadDeprecationFormState(defaultVersion);

    var section = $(".page-manage-deprecation")[0];
    // Only apply bindings if the form exists.
    // In certain situations (the package is locked, user doesn't have permissions, etc) we do not render the form.
    if (section) {
        ko.applyBindings(this, section);
    }
}