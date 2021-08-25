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

    // The V3 registration API links to the display package page's README using
    // the 'show-readme-container' URL fragment.
    var restorePreferredBodyTab = true;
    if (window.location.hash === '#show-readme-container') {
        $('#readme-body-tab').focus();
        restorePreferredBodyTab = false;
    }

    if (storage) {
        // Restore preferred package manager selection from localStorage.
        var preferredPackageManagerId = storage.getItem(packageManagerStorageKey);
        if (preferredPackageManagerId) {
            updatePackageManager(preferredPackageManagerId, true);
        }

        // Restore preferred body tab selection from localStorage.
        if (restorePreferredBodyTab) {
            var preferredBodyTab = storage.getItem(bodyStorageKey);
            if (preferredBodyTab) {
                $('#' + preferredBodyTab).tab('show');
            }
        }
    }

    // Make sure we save the user's preferred body tab to localStorage.
    $('.body-tab').on('shown.bs.tab', function (e) {
        if (storage) {
            storage.setItem(bodyStorageKey, e.target.id);
        }

        window.nuget.sendMetric("ShowDisplayPackageTab", 1, {
            TabId: e.target.id,
            PackageId: packageId,
            PackageVersion: packageVersion
        });
    });

    packageManagerSelector.on('change', function (e) {
        var newIndex = e.target.selectedIndex;
        var newPackageManagerId = e.target[newIndex].value;

        updatePackageManager(newPackageManagerId, false);

        // Make sure we save the user's preferred package manager to localStorage.
        if (storage) {
            storage.setItem(packageManagerStorageKey, newPackageManagerId);
        }

        window.nuget.sendMetric("ShowInstallCommand", 1, {
            PackageManagerId: newPackageManagerId,
            PackageId: packageId,
            PackageVersion: packageVersion
        });
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
    copyButton.popover({
        trigger: 'manual',
        // Windows Narrator does not announce popovers' content. See: https://github.com/twbs/bootstrap/issues/18618
        // We can force Narrator to announce the content by changing
        // the popover's role from 'tooltip' to 'status'.
        // Modified from: https://github.com/twbs/bootstrap/blob/f17f882df292b29323f1e1da515bd16f326cee4a/js/popover.js#L28
        template: '<div class="popover" role="status"><div class="arrow"></div><h3 class="popover-title"></h3><div class="popover-content"></div></div>'
    });

    copyButton.click(function () {
        var text = $('#' + currentPackageManagerId + '-text').text().trim();
        window.nuget.copyTextToClipboard(text, copyButton);
        copyButton.popover('show');
        setTimeout(function () {
            copyButton.popover('destroy');
        }, 1000);
        window.nuget.sendMetric("CopyInstallCommand", 1, {
            ButtonId: currentPackageManagerId,
            PackageId: packageId,
            PackageVersion: packageVersion
        });
    });

    if (window.nuget.isGaAvailable()) {
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
