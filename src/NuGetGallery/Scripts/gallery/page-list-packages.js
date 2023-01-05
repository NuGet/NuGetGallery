$(function() {
    'use strict';

    $(".reserved-indicator").each(window.nuget.setPopovers);

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

        if (this.checked == true) {
            updateFrameworkFilters("frameworks", this.id, true);
        } else {
            updateFrameworkFilters("frameworks", this.id, false);
        }

        const tfms = document.querySelectorAll('[parent=' + this.id);
        tfms.forEach((tfm) => {
            tfm.checked = false;
            updateFrameworkFilters("tfms", tfm.id, false);
        });
    }

    function clickTfmCheckbox() {
        const framework = document.getElementById(this.getAttribute('parent'));
        const tfms = document.querySelectorAll('[parent=' + this.getAttribute('parent') + ']');

        let checkedCount = 0;
        for (const tfm of tfms) {
            if (tfm.checked) {
                checkedCount++;
            }
        }

        framework.checked = false;
        updateFrameworkFilters("frameworks", framework.id, false);

        if (this.checked == true) {
            framework.indeterminate = true;
            updateFrameworkFilters("tfms", this.id, true);
        } else {
            if (checkedCount === 0) {
                framework.indeterminate = false;
            }
            updateFrameworkFilters("tfms", this.id, false);
        }
    }

    // Update the query string with the selected Frameworks and Tfms
    function updateFrameworkFilters(fieldName, frameworkName, add) {
        var searchField;
        if (fieldName == "frameworks") {
            searchField = searchForm.frameworks;
        }
        else if (fieldName == "tfms") {
            searchField = searchForm.tfms;
        }

        if (add) {
            searchField.value += frameworkName + ",";
        }
        else {
            searchField.value = searchField.value.replace(frameworkName + ",", "")
        }
    }

    // Initialize state for framework and tfm checkboxes
    initializeFrameworkAndTfmCheckboxes();
    function initializeFrameworkAndTfmCheckboxes() {
        var inputFrameworkFilters = searchForm.frameworks.value.split(',').map(f => f.trim()).filter(f => f);
        var inputTfmFilters = searchForm.tfms.value.split(',').map(f => f.trim()).filter(f => f);
        searchForm.frameworks.value = "";
        searchForm.tfms.value = "";

        for (const framework of inputFrameworkFilters) {
            const checkbox = document.getElementById(framework);

            if (checkbox) {
                checkbox.click();
            }
        }

        for (const tfm of inputTfmFilters) {
            const checkbox = document.getElementById(tfm);

            if (checkbox) {
                checkbox.click();
            }
        }
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

        this.classList.toggle('active');
        expandButton.classList.toggle('ms-Icon--ChevronDown');
        expandButton.classList.toggle('ms-Icon--ChevronUp');
        if (this.classList.contains('active')) {
            dataTab.style.maxHeight = dataTab.scrollHeight + "px";
        } else {
            dataTab.style.maxHeight = 0;
        }
    }
});
