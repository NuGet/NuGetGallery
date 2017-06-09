$(function () {
    'use strict';
    
    window.nuget.configureExpander("hidden-versions", "Show less", "Show more");
    window.nuget.configureExpander("dependency-groups", "Hide", "Show");

    // Copy button.
    var copyButton = $('#install-script-button');
    copyButton.popover({ trigger: 'manual' });

    $('#install-script-button').click(function () {
        var text = $('#install-script-text').text().trim();
        window.nuget.copyTextToClipboard(text);
        copyButton.popover('show');
        setTimeout(function () {
            copyButton.popover('destroy');
        }, 1000);
    });
});
