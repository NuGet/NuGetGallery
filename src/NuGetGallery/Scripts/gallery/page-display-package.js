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

        // The expander should not be clickable when it doesn't have content
        container.find('.deprecation-expander').removeAttr('role');

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
            return false;
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
    window.nuget.configureExpanderHeading("github-usage");
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

    var storage = window['localStorage'];
    if (storage) {
        var key = 'preferred_tab';

        // Restore preferred tab selection from localStorage.
        var preferredTab = storage.getItem(key);
        if (preferredTab) {
            $('#' + preferredTab).tab('show');
        }

        // Make sure we save the user's preferred tab to localStorage.
        $('.package-manager-tab').on('shown.bs.tab', function (e) {
            storage.setItem(key, e.target.id);
        });
    }

    if (window.nuget.isGaAvailable()) {
        // Emit a Google Analytics event when the user expands or collapses the Dependencies section.
        $("#dependency-groups").on('hide.bs.collapse show.bs.collapse', function (e) {
            ga('send', 'event', 'dependencies', e.type);
        });

        // Emit a Google Analytics event when the user expands or collapses the GitHub Usage section.
        $("#github-usage").on('hide.bs.collapse show.bs.collapse', function (e) {
            ga('send', 'event', 'github-usage', e.type);
        });

        // Emit a Google Analytics event when the user clicks on a repo link in the GitHub Usage section.
        $(".gh-link").on('click', function (elem) {
            if (!elem.delegateTarget.dataset.indexNumber) {
                console.error("indexNumber property doesn't exist!");
            } else {
                let linkIndex = elem.delegateTarget.dataset.indexNumber;
                ga('send', 'event', 'github-usage', 'link-click-' + linkIndex);
            }
        });
    }

    // Add smooth scrolling to dependent-repos-link
    $("#dependent-repos-link").on('click', function (event) {
        // Emit a Google Analytics event
        if (window.nuget.isGaAvailable()) {
            ga('send', 'event', 'github-usage', 'sidebar-link-click');
        }

        if (this.hash !== "") {
            event.preventDefault();
            let hash = this.hash;
            let hashElem = $(hash);
            if (hashElem.attr("aria-expanded") == "false") {
                hashElem.click();
            }
            $('html, body').animate({
                scrollTop: hashElem.offset().top
            }, 400, function () {
                // Add hash (#) to URL when done scrolling (default click behavior)
                window.location.hash = hash;
            });
        }
    });
});
