'use strict';

function ManageDeprecationSecurityDetailListItemViewModel(id, fromAutocomplete, name, description, cvss) {
    this.id = id;
    this.fromAutocomplete = fromAutocomplete;
    this.name = name;
    this.description = description;
    this.cvss = cvss;
}

// Shared model between the CVE view and the CWE view
function ManageDeprecationSecurityDetailListViewModel(id, title, label, placeholder, addLabel, addRegex, addErrorString, getUrlFromId, autocompleteUrl, processAutocompleteResult, updateCvssFromItem, allowMissingFromAutocomplete, missingFromAutocompleteErrorTemplate, missingAutocompleteName, missingAutocompleteDescription) {
    var self = this;

    this.id = id;
    this.title = title;
    this.label = label;
    this.placeholder = placeholder;
    this.getUrlFromId = getUrlFromId;
    this.missingAutocompleteName = missingAutocompleteName;
    this.missingAutocompleteDescription = missingAutocompleteDescription;

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
    this.addError = ko.observable('');

    this.showAutocompleteResults = ko.observable(true);
    var autocompleteSelector = "#" + id + "-autocomplete";
    window.nuget.configureDropdown(
        ":has(> " + autocompleteSelector + ")",
        autocompleteSelector,
        self.showAutocompleteResults,
        true);

    this.autocompleteResults = ko.observableArray();
    this.addId.subscribe(function () {
        self.addError('');

        var query = self.addId();
        $.ajax({
            url: autocompleteUrl,
            dataType: 'json',
            type: 'GET',
            data: {
                query: query
            },

            success: function (data) {
                if (query !== self.addId()) {
                    // Don't set the autocomplete results if the ID in the box has changed.
                    return;
                }

                if (!data.Success) {
                    self.autocompleteResults([]);
                    return;
                }

                self.autocompleteResults(
                    data.Results.map(processAutocompleteResult));
            },

            error: function () {
                if (query !== self.addId()) {
                    // Don't set the autocomplete results if the ID in the box has changed.
                    return;
                }

                self.autocompleteResults([]);
            }
        });
    }, this);

    this.add = function (addedItemViewModel) {
        var id = addedItemViewModel.id;
        if (!id.match(addRegex)) {
            self.addError("'" + id + "' is not a valid ID! " + addErrorString);
            return;
        }

        if (ko.utils.arrayFirst(self.addedIds(), function (item) { return item.id === id; })) {
            self.addError("'" + id + "' has already been added!");
            return;
        }

        self.addedIds.push(addedItemViewModel);
        self.addId('');
        updateCvssFromItem(addedItemViewModel);

        $(autocompleteSelector).find('[name="addId"]').focus();
    };

    this.addWithAutocomplete = function (item) {
        self.add(item);
    };

    this.addWithoutAutocomplete = function () {
        // Uppercase the ID because CVE and CWE IDs are case-insensitive.
        // If the user enters 'cve-2019-0001' instead of 'CVE-2019-0001', we shouldn't fail.
        var addedId = self.addId().toUpperCase();

        // If there is an autocomplete result with the same ID, use it.
        var matchingAutocompleteResult = ko.utils.arrayFirst(
            self.autocompleteResults(),
            function (result) { return result.id === addedId; });

        var addedItem;
        if (matchingAutocompleteResult) {
            addedItem = matchingAutocompleteResult;
        } else {
            if (!allowMissingFromAutocomplete) {
                self.addError(window.nuget.formatString(
                    missingFromAutocompleteErrorTemplate,
                    addedId));
                return;
            }

            addedItem = new ManageDeprecationSecurityDetailListItemViewModel(addedId, false);
        }

        self.add(addedItem);
    };

    this.addWithoutAutocompleteKeyDown = function (data, event) {
        if (event.which === 13) { /* Enter */
            self.addWithoutAutocomplete();
            return false;
        }

        return true;
    };

    this.remove = function (id, event) {
        var $target = $(event.target);

        // Try to focus on the next added item.
        var nextItem = $target.closest('.security-detail-list-item').next('.security-detail-list-item');
        if (nextItem.length) {
            nextItem.find(':tabbable').focus();
        } else {
            // Otherwise, focus on the "add item" input.
            $target.closest('.security-detail').find('[name="addId"]').focus();
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

    this.exportIds = function () {
        return self.export().map(function (item) { return item.id; });
    };
}

function ManageDeprecationViewModel(id, versionDeprecationStateDictionary, defaultVersion, submitUrl, packageUrl, getAlternatePackageVersionsUrl, cveUrlTemplate, getCveIdsUrl, cweUrlTemplate, getCweIdsUrl) {
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
    this.chosenItemsConflictWarning = ko.pureComputed(function () {
        var chosenItems = self.dropdown.chosenItems();
        var isVulnerable = self.isVulnerable();
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

            if (versionData.IsVulnerable || versionData.IsLegacy || versionData.IsOther) {
                hasVersionsWithExistingDeprecationState = true;
                break;
            }
        }

        if (isVulnerable || isLegacy || isOther) {
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

    this.isVulnerable = ko.observable(false);
    this.isLegacy = ko.observable(false);
    this.isOther = ko.observable(false);

    // Whether or not the checkbox for the CVSS section is checked.
    this.hasCvss = ko.observable(false);

    // The CVSS rating entered by the user.
    this.selectedCvssRating = ko.observable(0);

    // A string describing the severity of the CVSS rating entered by the user.
    var invalidCvssRatingString = 'Invalid CVSS rating!';
    this.getCvssRatingFloat = function () {
        var rating = self.selectedCvssRating();
        if (!rating) {
            return null;
        }

        var ratingFloat = parseFloat(rating);
        if (isNaN(ratingFloat) || ratingFloat < 0 || ratingFloat > 10) {
            return;
        }

        return ratingFloat;
    };

    this.cvssRatingLabel = ko.pureComputed(function () {
        var ratingFloat = self.getCvssRatingFloat();
        if (ratingFloat === null) {
            return '';
        }

        if (!ratingFloat) {
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
    
    this.chosenCvssRating = ko.pureComputed(function () {
        if (self.hasCvss()) {
            return self.selectedCvssRating();
        } else {
            // If the CVSS section is unchecked, there is no CVSS rating chosen.
            return null;
        }
    }, this);

    // The CVSS rating to submit with the form.
    this.cvssRating = ko.pureComputed(function () {
        if (self.isVulnerable()) {
            return self.chosenCvssRating();
        } else {
            // If the package is not vulnerable, don't submit the CVSS rating with the form.
            return null;
        }
    }, this);

    this.updateCvssFromItem = function (item) {
        if (!item || !item.cvss) {
            return;
        }

        self.hasCvss(true);

        var currentCvss = self.getCvssRatingFloat();
        var newCvss = currentCvss
            // If there is an existing CVSS, take the max of the current CVSS and the item's CVSS
            ? Math.max(currentCvss, item.cvss)
            // Otherwise, take the item's CVSS
            : item.cvss;
        self.selectedCvssRating(newCvss);
    };

    // The model for the CVEs view.
    this.chosenCves = new ManageDeprecationSecurityDetailListViewModel(
        "cve",
        "CVE ID(s)",
        "Add one or more CVEs applicable to the vulnerability.",
        "Add CVE by ID e.g. CVE-2014-999999, CVE-2015-888888",
        "Add CVE",
        /^CVE-\d{4}-\d{4,}$/g,
        "CVE IDs have the form 'CVE-YYYY-NNNN', where 'YYYY' is a year (exactly 4 digits) and 'NNNN' is a number (with at least 4 digits).",
        function (id) {
            return window.nuget.formatString(cveUrlTemplate, id);
        },
        getCveIdsUrl,
        function (result) {
            return new ManageDeprecationSecurityDetailListItemViewModel(
                result.CveId, true, null, result.Description, result.CvssRating);
        },
        this.updateCvssFromItem,
        true,
        null,
        "We could not find this CVE. Is it correct?",
        "NuGet.org refreshes its CVE data often and if we find this ID, your deprecation will be updated with the latest data.");

    this.cves = ko.pureComputed(function () {
        if (self.isVulnerable()) {
            return self.chosenCves.exportIds();
        } else {
            // If the package is not vulnerable, do not submit the CVEs with the form.
            return [];
        }
    }, this);

    // The model for the CWEs view
    this.chosenCwes = new ManageDeprecationSecurityDetailListViewModel(
        "cwe",
        "CWE(s)",
        "Add one or more CWEs applicable to the vulnerability.",
        "Add CWE by ID or title",
        "Add CWE",
        /^CWE-\d+$/g,
        "CWE IDs have the form 'CWE-N', where N is a number.",
        function (id) {
            return window.nuget.formatString(cweUrlTemplate, id.replace("CWE-", ""));
        },
        getCweIdsUrl,
        function (result) {
            return new ManageDeprecationSecurityDetailListItemViewModel(
                result.CweId, true, result.Name, result.Description, result.CvssRating);
        },
        this.updateCvssFromItem,
        false,
        "We could not find a CWE with an ID of '{0}'. NuGet.org refreshes its CWE information often, but we might not have the latest data. Please enter a different CWE or wait and try again later.",
        null,
        null);

    this.cwes = ko.pureComputed(function () {
        if (self.isVulnerable()) {
            return self.chosenCwes.exportIds();
        } else {
            // If the package is not vulnerable, do not submit the CWEs with the form.
            return [];
        }
    }, this);

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
                isVulnerable: self.isVulnerable(),
                isLegacy: self.isLegacy(),
                isOther: self.isOther(),
                cveIds: self.cves(),
                cvssRating: self.cvssRating(),
                cweIds: self.cwes(),
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

        versionData.IsVulnerable = self.isVulnerable();
        versionData.IsLegacy = self.isLegacy();
        versionData.IsOther = self.isOther();
        versionData.CveIds = self.chosenCves.export();
        versionData.CvssRating = self.chosenCvssRating();
        versionData.CweIds = self.chosenCwes.export();
        versionData.AlternatePackageId = self.alternatePackageId();
        versionData.AlternatePackageVersion = self.alternatePackageVersion();
        versionData.CustomMessage = self.customMessage();
    };

    var loadDeprecationFormState = function (version) {
        var versionData = versionDeprecationFormState[version];
        if (!versionData) {
            return;
        }

        self.isVulnerable(versionData.IsVulnerable);
        self.isLegacy(versionData.IsLegacy);
        self.isOther(versionData.IsOther);

        self.chosenCves.import(versionData.CveIds);

        self.hasCvss(!!versionData.CvssRating);
        self.selectedCvssRating(versionData.CvssRating);

        self.chosenCwes.import(versionData.CweIds);

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