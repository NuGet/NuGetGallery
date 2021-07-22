$(function () {
    'use strict';

    // Configure the rename information container
    window.nuget.configureExpander("rename-content-container", "ChevronDown", null, "ChevronUp");
    configureExpanderWithEnterKeydown($('#show-rename-content-container'));
    var expanderAttributes = ['data-toggle', 'data-target', 'aria-expanded', 'aria-controls', 'tabindex'];

    // Configure the vulnerability information container
    var vulnerabilitiesContainer = $('#show-vulnerabilities-content-container');
    if ($('#vulnerabilities-content-container').children().length) {
        // If the vulnerability information container has content, configure it as an expander.
        window.nuget.configureExpander("vulnerabilities-content-container", "ChevronDown", null, "ChevronUp");
        configureExpanderWithEnterKeydown(vulnerabilitiesContainer);
    } else {
        // If the container does not have content, remove its expander attributes
        expanderAttributes.forEach(attribute => vulnerabilitiesContainer.removeAttr(attribute));

        // The expander should not be clickable when it doesn't have content
        vulnerabilitiesContainer.find('.vulnerabilities-expander').removeAttr('role');

        $('#vulnerabilities-expander-icon-right').hide();
    }

    // Configure the deprecation information container
    var deprecationContainer = $('#show-deprecation-content-container');
    if ($('#deprecation-content-container').children().length) {
        // If the deprecation information container has content, configure it as an expander.
        window.nuget.configureExpander("deprecation-content-container", "ChevronDown", null, "ChevronUp");
        configureExpanderWithEnterKeydown(deprecationContainer);
    } else {
        // If the container does not have content, remove its expander attributes
        expanderAttributes.forEach(attribute => deprecationContainer.removeAttr(attribute));

        // The expander should not be clickable when it doesn't have content
        deprecationContainer.find('.deprecation-expander').removeAttr('role');

        $('#deprecation-expander-icon-right').hide();
    }

    // Configure expander with enter keydown event
    function configureExpanderWithEnterKeydown(container) {
        container.keydown(function (event) {
            if (event.which === 13) { // Enter
                $(event.target).click();
            }
        });
    }

    // Set up our state for the currently selected package manager.
    var currentPackageManagerId = packageManagers[0];
    var packageManagerSelector = $('.installation-instructions-dropdown');

    // Restore previously selected package manager and body tab.
    var storage = window['localStorage'];
    var packageManagerStorageKey = 'preferred_package_manager';
    var bodyStorageKey = 'preferred_body_tab';

    if (storage) {
        // Restore preferred package manager selection from localStorage.
        var preferredPackageManagerId = storage.getItem(packageManagerStorageKey);
        if (preferredPackageManagerId) {
            updatePackageManager(preferredPackageManagerId, true);
        }

        // Restore preferred body tab selection from localStorage.
        var preferredBodyTab = storage.getItem(bodyStorageKey);
        if (preferredBodyTab) {
            $('#' + preferredBodyTab).tab('show');
        }

        // Make sure we save the user's preferred body tab to localStorage.
        $('.body-tab').on('shown.bs.tab', function (e) {
            storage.setItem(bodyStorageKey, e.target.id);
        });
    }

    packageManagerSelector.on('change', function (e) {
        var newIndex = e.target.selectedIndex;
        var newPackageManagerId = e.target[newIndex].value;

        updatePackageManager(newPackageManagerId, false);

        // Make sure we save the user's preferred package manager to localStorage.
        if (storage) {
            storage.setItem(packageManagerStorageKey, currentPackageManagerId);
        }
    });

    // Used to switch installation instructions when a new package manager is selected 
    function updatePackageManager(newPackageManagerId, updateSelector) {
        var currentInstructions = $('#' + currentPackageManagerId + '-instructions');
        var newInstructions = $('#' + newPackageManagerId + '-instructions');

        // Ignore if the new instructions do not exist. This may happen if we restore
        // a preferred package manager that has been renamed or removed. 
        if (newInstructions.length === 0) {
            return;
        }

        currentInstructions.addClass('hidden');
        newInstructions.removeClass('hidden');

        currentPackageManagerId = newPackageManagerId;

        if (updateSelector) {
            packageManagerSelector[0].value = preferredPackageManagerId;
        }
    }

    // Configure package manager copy button
    var copyButton = $('.installation-instructions button');
    copyButton.popover({ trigger: 'manual' });

    copyButton.click(function () {
        var text = $('#' + currentPackageManagerId + '-text').text().trim();
        window.nuget.copyTextToClipboard(text, copyButton);
        copyButton.popover('show');
        //This is workaround for Narrator announce the status changes of copy button to achieve accessibility.
        copyButton.attr('aria-pressed', 'true');
        setTimeout(function () {
            copyButton.popover('destroy');
        }, 1000);
        setTimeout(function () {
            copyButton.attr('aria-pressed', 'false');
        }, 1500);
        window.nuget.sendMetric("CopyInstallCommand", 1, {
            ButtonId: currentPackageManagerId,
            PackageId: packageId,
            PackageVersion: packageVersion
        });
    });

    if (window.nuget.isGaAvailable()) {
        // TO-DO add telemetry events for when each tab is clicked, see https://github.com/nuget/nugetgallery/issues/8613

        // Emit a Google Analytics event when the user clicks on a repo link in the GitHub Repos area of the Used By section.
        $(".gh-link").on('click', function (elem) {
            if (!elem.delegateTarget.dataset.indexNumber) {
                console.error("indexNumber property doesn't exist!");
            } else {
                var linkIndex = elem.delegateTarget.dataset.indexNumber;
                ga('send', 'event', 'github-usage', 'link-click-' + linkIndex);
            }
        });

        // Emit a Google Analytics event when the user clicks on a package link in the NuGet Packages area of the Used By section.
        $(".ngp-link").on('click', function (elem) {
            if (!elem.delegateTarget.dataset.indexNumber) {
                console.error("indexNumber property doesn't exist!");
            } else {
                var linkIndex = elem.delegateTarget.dataset.indexNumber;
                ga('send', 'event', 'used-by-packages', 'link-click-' + linkIndex);
            }
        });
    }
});
