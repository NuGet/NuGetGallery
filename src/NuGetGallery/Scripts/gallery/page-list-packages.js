$(function() {
    'use strict';

    $(".reserved-indicator").each(window.nuget.setPopovers);
    $(".framework-filter-info-icon").each(window.nuget.setPopovers);

    const searchForm = document.forms.search;
    const allFrameworks = document.querySelectorAll('.framework');
    const allTfms = document.querySelectorAll('.tfm');

    // Hide the default search bar in the page header
    const defaultSearchBarHeader = document.getElementById("search-bar-header");
    defaultSearchBarHeader.parentNode.removeChild(defaultSearchBarHeader);

    // Checkbox logic for Framework and Tfm filters
    for (const framework of allFrameworks) {
        framework.addEventListener('click', clickFrameworkCheckbox);
    }

    for (const tfm of allTfms) {
        tfm.addEventListener('click', clickTfmCheckbox);
    }

    function clickFrameworkCheckbox() {
        this.indeterminate = false;
        updateFrameworkFilters(searchForm.frameworks, this.id, this.checked);

        const tfms = document.querySelectorAll('[parent=' + this.id + ']');
        tfms.forEach((tfm) => {
            tfm.checked = false;
            updateFrameworkFilters(searchForm.tfms, tfm.id, false);
        });
    }

    function clickTfmCheckbox() {
        const framework = document.getElementById(this.getAttribute('parent'));
        const tfms = document.querySelectorAll('[parent=' + this.getAttribute('parent') + ']');

        const checkedCount = Array.from(tfms).reduce((accumulator, tfm) => accumulator + (tfm.checked ? 1 : 0), 0);

        framework.checked = false;
        framework.indeterminate = checkedCount !== 0;
        updateFrameworkFilters(searchForm.frameworks, framework.id, false);

        updateFrameworkFilters(searchForm.tfms, this.id, this.checked);
    }

    // Update the query string with the selected Frameworks and Tfms
    function updateFrameworkFilters(searchField, frameworkName, add) {
        if (add) {
            searchField.value += frameworkName + ",";
        }
        else {
            searchField.value = searchField.value.replace(frameworkName + ",", "")
        }
    }

    // Initialize state for Framework and Tfm checkboxes
    // NOTE: We first click on all selected Framework checkboxes and then on the selected Tfm checkboxes, which
    // allows us to correctly handle cases where a Framework AND one of its child Tfms is present in the query
    initializeFrameworkAndTfmCheckboxes();
    function initializeFrameworkAndTfmCheckboxes() {
        var inputFrameworkFilters = searchForm.frameworks.value.split(',').map(f => f.trim()).filter(f => f);
        var inputTfmFilters = searchForm.tfms.value.split(',').map(f => f.trim()).filter(f => f);
        searchForm.frameworks.value = "";
        searchForm.tfms.value = "";

        inputFrameworkFilters.map(id => document.getElementById(id)).forEach(checkbox => checkbox.click());
        inputTfmFilters.map(id => document.getElementById(id)).forEach(checkbox => checkbox.click());
    }

    // Submit the form when a user changes the selected 'sortBy' option
    searchForm.sortby.addEventListener('change', (e) => {
        searchForm.sortby.value = e.target.value;
        searchForm.submit();
    });

    // Accordion/collapsible logic
    const collapsibles = document.querySelectorAll('.collapsible');

    for (const collapsible of collapsibles) {
        collapsible.addEventListener('click', toggleCollapsible);
    }

    function toggleCollapsible() {
        var dataTab = document.getElementById(this.getAttribute('tab') + 'tab');
        var expandButton = document.getElementById(this.getAttribute('tab') + 'button');
        const tfmCheckboxes = dataTab.querySelectorAll('[parent=' + this.getAttribute('tab') + ']');

        this.classList.toggle('active');
        expandButton.classList.toggle('ms-Icon--ChevronDown');
        expandButton.classList.toggle('ms-Icon--ChevronUp');

        if (this.classList.contains('active')) {
            dataTab.style.maxHeight = dataTab.scrollHeight + "px";

            for (const tfm of tfmCheckboxes) {
                tfm.setAttribute('tabindex', '0');
                tfm.tabindex = "0";
            }
        }
        else {
            dataTab.style.maxHeight = 0;

            for (const tfm of tfmCheckboxes) {
                tfm.setAttribute('tabindex', '-1');
                tfm.tabindex = "-1";
            }
        }
    }
});
