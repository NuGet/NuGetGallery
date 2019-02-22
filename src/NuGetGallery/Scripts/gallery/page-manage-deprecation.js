'use strict';

// Shared model between the CVE view and the CWE view
function ManageDeprecationSecurityDetailListViewModel(title, label, placeholder, addLabel) {
    var self = this;

    this.title = ko.observable(title);
    this.label = ko.observable(label);
    this.placeholder = ko.observable(placeholder);

    // Whether or not the checkbox for this section is checked.
    this.hasIds = ko.observable(false);

    // The IDs that the user has added to this form.
    this.addedIds = ko.observableArray();
    // The IDs to submit with the form.
    this.ids = ko.pureComputed(function () {
        if (self.hasIds()) {
            return self.addedIds();
        } else {
            // If the checkbox for this section is not selected, do not return the added IDs.
            return [];
        }
    }, this);

    // The ID that has been typed into the textbox but not yet submitted.
    this.addId = ko.observable('');
    this.addLabel = addLabel;
    this.add = function () {
        self.addedIds.push(self.addId());
        self.addId('');
    };
    this.addKeyDown = function (data, event) {
        if (event.which === 13) { /* Enter */
            self.add();
            return false;
        }

        return true;
    };

    this.remove = function (id, event) {
        // Try to focus on the next added item.
        var nextItem = $(event.target).closest('.security-detail-list-item').next('.security-detail-list-item');
        if (nextItem.length) {
            nextItem.find(':tabbable').focus();
        } else {
            // Otherwise, focus on the "add item" input.
            $(event.target).closest('.security-detail').find('[name="addId"]').focus();
        }

        self.addedIds.remove(id);
    };

    // Import the existing version deprecation state into this model.
    this.import = function (ids) {
        var hasIds = ids && ids.length;
        self.hasIds(hasIds);
        if (hasIds) {
            self.addedIds(ids);
        } else {
            self.addedIds.removeAll();
        }
    };

    // Export this model into an array of IDs.
    this.export = function () {
        // Copy the array. 
        // Otherwise, the value returned by this function will change based on the UI.
        return self.ids().slice(0);
    };
}

function ManageDeprecationViewModel(id, versionDeprecationStateDictionary, defaultVersion, submitUrl, packageUrl, getAlternatePackageVersions) {
    var self = this;

    // Existing deprecation state information per version.
    var items = Object.keys(versionDeprecationStateDictionary).map(function (version) {
        var versionData = versionDeprecationStateDictionary[version];
        return new MultiSelectDropdownItem(
            version,
            versionData.Text,
            version,
            version === defaultVersion,
            versionData.IsVulnerable || versionData.IsLegacy || versionData.IsOther);
    });

    this.dropdown = new MultiSelectDropdown(items, "version", "versions");

    this.isVulnerable = ko.observable(false);
    this.isLegacy = ko.observable(false);
    this.isOther = ko.observable(false);

    // The model for the CVEs view.
    this.cves = new ManageDeprecationSecurityDetailListViewModel(
        "CVE ID(s)",
        "Add one or more CVEs applicable to the vulnerability.",
        "Add CVE by ID e.g. CVE-2014-999999, CVE-2015-888888",
        "Add CVE");

    // Whether or not the checkbox for the CVSS section is checked.
    this.hasCvss = ko.observable(false);

    // The CVSS rating entered by the user.
    this.selectedCvssRating = ko.observable(0);

    // A string describing the severity of the CVSS rating entered by the user.
    var invalidCvssRatingString = 'Invalid CVSS rating!';
    this.cvssRatingLabel = ko.pureComputed(function () {
        var rating = self.selectedCvssRating();
        if (!rating) {
            return '';
        }

        var ratingFloat = parseFloat(rating);
        if (isNaN(ratingFloat) || ratingFloat < 0 || ratingFloat > 10) {
            return invalidCvssRatingString;
        }

        if (ratingFloat < 4) {
            return 'Low';
        }

        if (ratingFloat < 7) {
            return 'Medium';
        }

        if (ratingFloat < 9) {
            return 'High';
        }

        return 'Critical';
    }, this);
    this.cvssRatingIsInvalid = ko.pureComputed(function () {
        return self.cvssRatingLabel() === invalidCvssRatingString;
    }, this);

    // The CVSS rating to submit with the form.
    this.cvssRating = ko.pureComputed(function () {
        if (self.hasCvss()) {
            return self.selectedCvssRating();
        } else {
            // If the CVSS section is unchecked, don't submit the CVSS rating with the form.
            return null;
        }
    }, this);

    // The model for the CWEs view
    this.cwes = new ManageDeprecationSecurityDetailListViewModel(
        "CWE(s)",
        "Add one or more CWEs applicable to the vulnerability.",
        "Add CWE by ID or title",
        "Add CWE");

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
            url: getAlternatePackageVersions,
            dataType: 'json',
            type: 'GET',
            data: {
                id: id
            },

            statusCode: {
                200: function (data) {
                    if (self.alternatePackageId() === id) {
                        self.alternatePackageVersionsCached(data);
                        self.chosenAlternatePackageIdError(null);
                    }
                },

                404: function () {
                    if (self.alternatePackageId() === id) {
                        self.alternatePackageVersionsCached.removeAll();
                        self.chosenAlternatePackageIdError("Could not find alternate package '" + id + "'.");
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

    // Whether or not the packages should be unlisted.
    this.shouldUnlist = ko.observable(true);

    this.submitError = ko.observable();
    this.submit = function () {
        self.submitError(null);

        var chosenItems = self.dropdown.chosenItems();
        var isVulnerable = self.isVulnerable();
        var isLegacy = self.isLegacy();
        var isOther = self.isOther();
        var alertMessage = "";
        var areMultipleVersionsSelected = chosenItems.length > 1;

        if (isVulnerable || isLegacy || isOther) {
            if (areMultipleVersionsSelected) {
                var warnAboutReplacing = false;
                for (var i in chosenItems) {
                    var version = chosenItems[i];
                    var versionData = versionDeprecationStateDictionary[version];
                    if (!versionData) {
                        // It shouldn't be possible to select a version that didn't exist when the page loaded.
                        // In case there is a bug and the user did select a valid version, continue on anyway.
                        continue;
                    }

                    if (isVulnerable && versionData.IsVulnerable || isLegacy && versionData.IsLegacy) {
                        warnAboutReplacing = true;
                        break;
                    }
                }

                if (warnAboutReplacing) {
                    var informationToReplace = "";
                    if (isVulnerable) {
                        informationToReplace += "vulnerability";
                    }

                    if (isLegacy) {
                        if (isVulnerable) {
                            informationToReplace += " and ";
                        }

                        informationToReplace += "alternate package";
                    }

                    alertMessage = "Some of your versions will have their" + informationToReplace + " information replaced.";
                } else {
                    alertMessage = "Some of your versions will have this new deprecation information added to their existing information.";
                }
            }
        } else {
            alertMessage = "The version" + (areMultipleVersionsSelected > 1 ? "s" : "") +
                " you selected will be un-deprecated and have their deprecation information removed.";
        }

        if (alertMessage && !confirm(alertMessage + " Do you want to continue?")) {
            return;
        }

        $.ajax({
            url: submitUrl,
            dataType: 'json',
            type: 'POST',
            data: window.nuget.addAjaxAntiForgeryToken({
                id: id,
                versions: chosenItems,
                isVulnerable: isVulnerable,
                isLegacy: isLegacy,
                isOther: isOther,
                cveIds: self.cves.export(),
                cvssRating: self.cvssRating(),
                cweIds: self.cwes.export(),
                alternatePackageId: self.alternatePackageId(),
                alternatePackageVersion: self.alternatePackageVersion(),
                customMessage: self.customMessage(),
                shouldUnlist: self.shouldUnlist()
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

        versionData.IsVulnerable = self.isVulnerable();
        versionData.IsLegacy = self.isLegacy();
        versionData.IsOther = self.isOther();
        versionData.CVEIds = self.cves.export();
        versionData.CVSSRating = self.cvssRating();
        versionData.CWEIds = self.cwes.export();
        versionData.AlternatePackageId = self.alternatePackageId();
        versionData.AlternatePackageVersion = self.alternatePackageVersion();
        versionData.CustomMessage = self.customMessage();
        versionData.ShouldUnlist = self.shouldUnlist();
    };

    var loadDeprecationFormState = function (version) {
        var versionData = versionDeprecationFormState[version];
        if (!versionData) {
            return;
        }

        self.isVulnerable(versionData.IsVulnerable);
        self.isLegacy(versionData.IsLegacy);
        self.isOther(versionData.IsOther);

        self.cves.import(versionData.CVEIds);

        self.hasCvss(versionData.CVSSRating);
        self.selectedCvssRating(versionData.CVSSRating);

        self.cwes.import(versionData.CWEIds);

        self.chosenAlternatePackageId(versionData.AlternatePackageId);
        if (versionData.AlternatePackageVersion) {
            self.alternatePackageVersionsCached([versionData.AlternatePackageVersion]);
            self.chosenAlternatePackageVersion(versionData.AlternatePackageVersion);
        }

        self.customMessage(versionData.CustomMessage);
        self.shouldUnlist(versionData.ShouldUnlist);
    };

    // When the chosen versions are changed, remember the contents of the form in case the user navigates back to this version.
    this.dropdown.chosenItems.subscribe(function (oldVersions) {
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

    ko.applyBindings(this, $(".page-manage-deprecation")[0]);
}