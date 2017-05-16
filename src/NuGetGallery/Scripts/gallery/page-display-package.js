$(function () {
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
