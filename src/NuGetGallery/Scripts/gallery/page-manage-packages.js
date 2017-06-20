$(function () {
    'use strict';

    window.nuget.configureExpander(
        "packages-Published",
        "ChevronRight",
        "My Published Packages",
        "ChevronDown",
        "My Published Packages");
    window.nuget.configureExpander(
        "packages-Unlisted",
        "ChevronRight",
        "My Unlisted Packages",
        "ChevronDown",
        "My Unlisted Packages");
});
