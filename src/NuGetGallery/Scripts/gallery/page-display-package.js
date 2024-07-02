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

    // Configure package manager copy buttons
    function configureCopyButton(id) {
        var copyButton = $('#' + id + '-button');
        var copyButtonDom = copyButton.get(0);
        copyButton.popover({ trigger: 'manual' });

        copyButton.click(function () {
            var text = $('#' + id + '-text .install-command-row').text().trim();
            window.nuget.copyTextToClipboard(text, copyButton);

            copyButton.popover('show');

            // Windows Narrator does not announce popovers' content. See: https://github.com/twbs/bootstrap/issues/18618
            // We can force Narrator to announce the popover's content by "flashing"
            // the copy button's ARIA label.
            var originalLabel = copyButtonDom.ariaLabel;
            copyButtonDom.ariaLabel = "";

            setTimeout(function () {
                copyButton.popover('destroy');

                // We need to restore the copy button's original ARIA label.
                // Wait 0.15 seconds for the popover to fade away first.
                // Otherwise, the screen reader will re-announce the popover's content.
                setTimeout(function () {
                    copyButtonDom.ariaLabel = originalLabel;
                }, 200);
            }, 1500);

            window.nuget.sendMetric("CopyInstallCommand", 1, {
                ButtonId: id,
                PackageId: packageId,
                PackageVersion: packageVersion
            });
        });
    }

    for (var i in packageManagers)
    {
        configureCopyButton(packageManagers[i]);
    }

    // Restore previously selected package manager and body tab.
    var storage = window['localStorage'];
    var packageManagerStorageKey = 'preferred_package_manager';
    var bodyStorageKey = 'preferred_body_tab';
    var restorePreferredBodyTab = true;

    var windowHash = window.location.hash;
    if (windowHash) {
        // The V3 registration API links to the display package page's README using
        // the 'show-readme-container' URL fragment.
        if (windowHash === '#show-readme-container') {
            windowHash = '#readme-body-tab';
        }

        $(windowHash).focus();
        $(windowHash).tab('show');
        // don't restore body tab given the window hash
        restorePreferredBodyTab = false;
    }

    if (storage) {
        // Restore preferred package manager selection from localStorage.
        var preferredPackageManagerId = storage.getItem(packageManagerStorageKey);
        if (preferredPackageManagerId) {
            $('#' + preferredPackageManagerId).tab('show');
        }

        // Restore preferred body tab selection from localStorage.
        if (restorePreferredBodyTab) {
            var preferredBodyTab = storage.getItem(bodyStorageKey);
            if (preferredBodyTab) {
                $('#' + preferredBodyTab).tab('show');
            }
        }
    }

    var usedByClamped = false;
    var usedByTab = $('#usedby-tab');

    function clampUsedByDescriptions() {
        // Clamp long descriptions in the "used by" tab. Ensure this runs only once,
        // otherwise clamp.js removes too much content.
        if (usedByClamped) return;
        if (!usedByTab.hasClass('active')) return;

        for (var usedByDescription of $('.used-by-desc').get()) {
            $clamp(usedByDescription, { clamp: 2, useNativeClamp: false });
        }

        usedByClamped = true;
    }

    clampUsedByDescriptions();

    // Make sure we save the user's preferred body tab to localStorage.
    $('.package-manager-tab').on('shown.bs.tab', function (e) {
        if (storage) {
            storage.setItem(packageManagerStorageKey, e.target.id);
        }

        window.nuget.sendMetric("ShowInstallCommand", 1, {
            PackageManagerId: e.target.id,
            PackageId: packageId,
            PackageVersion: packageVersion
        });
    });

    $('.body-tab').on('shown.bs.tab', function (e) {
        if (storage) {
            storage.setItem(bodyStorageKey, e.target.id);
        }

        window.history.replaceState("", "", "#" + e.target.id);

        clampUsedByDescriptions();

        window.nuget.sendMetric("ShowDisplayPackageTab", 1, {
            TabId: e.target.id,
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

    $(".reserved-indicator").each(window.nuget.setPopovers);
    $(".framework-badge-asset").each(window.nuget.setPopovers);
    $(".package-warning-icon").each(window.nuget.setPopovers);
});
