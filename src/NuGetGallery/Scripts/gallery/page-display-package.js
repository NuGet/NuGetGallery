$(function () {
    'use strict';

    // Hidden versions.
    var hiddenVersions = $('#hidden-versions');
    var showHiddenVersion = $('#show-hidden-versions');
    var showHiddenVersionText = $('#show-hidden-versions span');
    var showHiddenVersionIcon = $('#show-hidden-versions i');
    hiddenVersions.on('hide.bs.collapse', function () {
        showHiddenVersionText.text('Show more');
        showHiddenVersionIcon.removeClass('fa-chevron-up');
        showHiddenVersionIcon.addClass('fa-chevron-down');
    });
    hiddenVersions.on('show.bs.collapse', function () {
        showHiddenVersionText.text('Show less');
        showHiddenVersionIcon.removeClass('fa-chevron-down');
        showHiddenVersionIcon.addClass('fa-chevron-up');
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
            copyButton.popover('hide');
        }, 1000);
    });
});
