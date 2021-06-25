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
    }
    else {
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

    var storage = window['localStorage'];
    var installationKey = 'preferred_instruction';

    var currentPackageManagerId = packageManagers[0];
    var packageManagerSelector = $('.installation-instructions-dropdown');

    if (storage) {
        var preferredPackageManagerId = storage.getItem(installationKey);
        if (preferredPackageManagerId) {
            updatePackageManager(preferredPackageManagerId);
            packageManagerSelector[0].value = preferredPackageManagerId;
        }

        // set preferred body tab 
        var bodyKey = 'preferred_body_tab';

        // Restore preferred body tab selection from localStorage.
        var preferredBodyTab = storage.getItem(bodyKey);
        if (preferredBodyTab) {
            $('#' + preferredBodyTab).tab('show');
        }

        // Make sure we save the user's preferred body tab to localStorage.
        $('.body-tab').on('shown.bs.tab', function (e) {
            storage.setItem(bodyKey, e.target.id);
        });
    }

    // Finds the selected package manager installation instructions
    packageManagerSelector.on('change', function (e) {
        var newIndex = e.target.selectedIndex;
        var newPackageManagerId = e.target[newIndex].value;

        updatePackageManager(newPackageManagerId);

        storage.setItem(installationKey, currentPackageManagerId);
    });

    // Used to switch installation instructions when a new package manager is selected 
    function updatePackageManager(newPackageManagerId) {
        var currentInstructionsId = '#' + currentPackageManagerId + '-instructions';
        var newInstructionsId = '#' + newPackageManagerId + '-instructions';

        $(currentInstructionsId).addClass('hidden');
        $(newInstructionsId).removeClass('hidden');

        currentPackageManagerId = newPackageManagerId;
    }

    // Configure package manager copy button
    var copyButton = $('.installation-instructions-buttons');
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
