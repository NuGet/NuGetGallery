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

    // Configure ReadMe container
    var readmeContainer = $("#readme-container");
    if (readmeContainer[0])
    {
        window.nuget.configureExpanderHeading("readme-container");

        window.nuget.configureExpander(
            "readme-more",
            "CalculatorAddition",
            "Show less",
            "CalculatorSubtract",
            "Show more");

        var showLess = $("#readme-less");
        $clamp(showLess[0], { clamp: 10, useNativeClamp: false });

        $("#show-readme-more").click(function (e) {
            showLess.collapse("toggle");
            e.preventDefault();
        });
        showLess.on('hide.bs.collapse', function (e) {
            e.stopPropagation();
        });
        showLess.on('show.bs.collapse', function (e) {
            e.stopPropagation();
        });
        return false;
    }

    // Configure expanders
    window.nuget.configureExpanderHeading("dependency-groups");
    window.nuget.configureExpanderHeading("used-by");
    window.nuget.configureExpanderHeading("version-history");
    window.nuget.configureExpander(
        "hidden-versions",
        "CalculatorAddition",
        "Show less",
        "CalculatorSubtract",
        "Show more"); 

    // Configure package manager copy buttons
    function configureCopyButton(id) {
        var copyButton = $('#' + id + '-button');
        copyButton.popover({ trigger: 'manual' });

        copyButton.click(function () {
            var text = $('#' + id + '-text').text().trim();
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

    // Enable the undo edit link.
    $("#undo-pending-edits").click(function (e) {
        e.preventDefault();
        $(this).closest('form').submit();
    })

    var storage = window['localStorage'];
    if (storage) {
        var key = 'preferred_tab';

        // Restore preferred tab selection from localStorage.
        var preferredTab = storage.getItem(key);
        if (preferredTab) {
            $('#' + preferredTab).tab('show');
        }

        // Make sure we save the user's preferred tab to localStorage.
        $('.package-manager-tab').on('shown.bs.tab', function (e) {
            storage.setItem(key, e.target.id);
        });
    }

    if (window.nuget.isGaAvailable()) {
        // Emit a Google Analytics event when the user expands or collapses the Dependencies section.
        $("#dependency-groups").on('hide.bs.collapse show.bs.collapse', function (e) {
            ga('send', 'event', 'dependencies', e.type);
        });

        // Emit a Google Analytics event when the user expands or collapses the Used By section.
        $("#used-by").on('hide.bs.collapse show.bs.collapse', function (e) {
            ga('send', 'event', 'used-by', e.type);
        });

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
