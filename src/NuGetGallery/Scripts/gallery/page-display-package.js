$(function () {
    'use strict';

    // Hidden versions.
    var hiddenVersions = $('#hidden-versions');
    var showHiddenVersion = $('#show-hidden-versions');
    var showHiddenVersionText = $('#show-hidden-versions span');
    var showHiddenVersionIcon = $('#show-hidden-versions i');
    hiddenVersions.on('hide.bs.collapse', function () {
        showHiddenVersionText.text('Show more');
        showHiddenVersionIcon.removeClass('ms-Icon--ChevronUp');
        showHiddenVersionIcon.addClass('ms-Icon--ChevronDown');
    });
    hiddenVersions.on('show.bs.collapse', function () {
        showHiddenVersionText.text('Show less');
        showHiddenVersionIcon.removeClass('ms-Icon--ChevronDown');
        showHiddenVersionIcon.addClass('ms-Icon--ChevronUp');
    });
    showHiddenVersion.on('click', function (e) {
        e.preventDefault();
    });

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
