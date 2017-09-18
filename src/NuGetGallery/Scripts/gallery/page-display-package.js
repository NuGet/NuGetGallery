$(function () {
    'use strict';

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
    
    var readmeContainer = $("#readme-container");
    if (readmeContainer[0])
    {
        window.nuget.configureExpanderHeading(
            "readme-container");   

        window.nuget.configureExpander(
            "readme-full",
            "CalculatorAddition",
            "Show less",
            "CalculatorSubtract",
            "Show more");
    }

    window.nuget.configureExpanderHeading("dependency-groups");
    window.nuget.configureExpanderHeading("version-history");
    window.nuget.configureExpander(
        "hidden-versions",
        "CalculatorAddition",
        "Show less",
        "CalculatorSubtract",
        "Show more");    

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
