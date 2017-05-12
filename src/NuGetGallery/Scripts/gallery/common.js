$(function () {
    // Show elements that require ClickOnce
    (function () {
        var userAgent = window.navigator.userAgent.toUpperCase();
        var hasNativeDotNet = userAgent.indexOf('.NET CLR 3.5') >= 0;
        if (hasNativeDotNet) {
            $('.no-clickonce').removeClass('no-clickonce');
        }
    })();
});
