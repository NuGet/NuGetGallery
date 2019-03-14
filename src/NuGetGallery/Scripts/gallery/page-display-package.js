$(function () {
    'use strict';

    // Configure the deprecation information container
    var container = $('#show-deprecation-content-container');
    if ($('#deprecation-content-container').children().length) {
        // If the deprecation information container has content, configure it as an expander.
        window.nuget.configureExpander("deprecation-content-container", "ChevronDown", null, "ChevronUp");
        container.keydown(function (event) {
            if (event.which === 13) { // Enter
                $(event.target).click();
            }
        });
    }
    else {
        // If the container does not have content, remove its expander attributes
        var expanderAttributes = ['data-toggle', 'data-target', 'aria-expanded', 'aria-controls', 'tabindex'];
        for (var i in expanderAttributes) {
            container.removeAttr(expanderAttributes[i]);
        }

        $('#deprecation-expander-icon-right').hide();
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

        $("#show-readme-more").click(function () {
            showLess.collapse("toggle");
        });
        showLess.on('hide.bs.collapse', function (e) {
            e.stopPropagation();
        });
        showLess.on('show.bs.collapse', function (e) {
            e.stopPropagation();
        });
    }

    // Configure expanders
    window.nuget.configureExpanderHeading("dependency-groups");
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
            setTimeout(function () {
                copyButton.popover('destroy');
            }, 1000);
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

    // Emit a Google Analytics event when the user expands or collapses the Dependencies section.
    if (window.nuget.isGaAvailable()) {
        $("#dependency-groups").on('hide.bs.collapse show.bs.collapse', function (e) {
            ga('send', 'event', 'dependencies', e.type);
        });
    }
});
