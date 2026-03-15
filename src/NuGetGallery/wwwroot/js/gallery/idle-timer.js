// User idle timer utility
(function () {
    'use strict';

    window.nuget = window.nuget || {};

    window.nuget.executeOnInactive = function (onTimeout, timeoutInMs) {
        var t;
        window.onload = resetTimer;
        document.onmousemove = resetTimer;
        document.onkeypress = resetTimer;

        function resetTimer() {
            clearTimeout(t);
            t = setTimeout(onTimeout, timeoutInMs);
        }
    };
})();
