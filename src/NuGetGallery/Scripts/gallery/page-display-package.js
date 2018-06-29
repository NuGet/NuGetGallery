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
        window.nuget.configureExpanderHeading("readme-container");
        window.nuget.configureExpanderLink("readme-more");

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

    window.nuget.configureExpanderHeading("related-packages");
    window.nuget.configureExpanderLink("hidden-packages");
    window.nuget.configureExpanderHeading("dependency-groups");
    window.nuget.configureExpanderHeading("version-history");
    window.nuget.configureExpanderLink("hidden-versions");

    for (var i in packageManagers)
    {
        configureCopyButton(packageManagers[i]);
    }

    // Enable the undo edit link.
    $("#undo-pending-edits").click(function (e) {
        e.preventDefault();
        $(this).closest('form').submit();
    })

    if (window.nuget.isGaAvailable()) {
        // Emit a Google Analytics event when the user expands or collapses the Dependencies section.
        $("#dependency-groups").on('hide.bs.collapse show.bs.collapse', function (e) {
            ga('send', 'event', 'dependencies', e.type);
        });

        // Fires when the user shows/hides the Related Packages section.
        $("#related-packages").on('hide.bs.collapse show.bs.collapse', function (e) {
            var action = (e.type === 'hide.bs.collapse' ? 'hide' : 'show');
            ga('send', 'event', 'related packages', action);
        });

        // Fires when the user clicks 'Show less/more' inside the Related Packages section.
        $("#hidden-packages").on('hide.bs.collapse show.bs.collapse', function (e) {
            var action = (e.type === 'hide.bs.collapse' ? 'show less' : 'show more');
            ga('send', 'event', 'related packages', action);
        });

        // Fires when the user clicks on the link to a related package.
        $("#related-packages .package .package-title").on('click', function (e) {
            var label = e.target.href;
            ga('send', 'event', 'related packages', 'click', label);
        });
    }
});
