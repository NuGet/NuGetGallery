$(function() {
    'use strict';

    $(".reserved-indicator").each(window.nuget.setPopovers);

    const defaultSearchBarHeader = document.getElementById("search-bar-header");
    defaultSearchBarHeader.innerHTML = "";
});
