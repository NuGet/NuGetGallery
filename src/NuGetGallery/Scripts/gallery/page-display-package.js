$(function () {
    'use strict';

    function configureCopyButton(id) {
        var copyButton = $('#' + id + '-button');
        copyButton.popover({ trigger: 'manual' });

        copyButton.click(function () {
            var text = $('#' + id + '-text').text().trim();
            window.nuget.copyTextToClipboard(text);
            copyButton.popover('show');
            setTimeout(function () {
                copyButton.popover('destroy');
            }, 1000);
        });
    }

    window.nuget.configureExpander(
        "dependency-groups",
        "ChevronRight",
        "Dependencies",
        "ChevronDown",
        "Dependencies");
    window.nuget.configureExpander(
        "hidden-version",
        "CalculatorAddition",
        "Show less",
        "CalculatorSubtract",
        "Show more");    
    window.nuget.configureExpander(
        "version-history",
        "ChevronRight",
        "Version History",
        "ChevronDown",
        "Version History");

    configureCopyButton('package-manager');
    configureCopyButton('dotnet-cli');
});
