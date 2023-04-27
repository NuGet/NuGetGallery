$(function() {
    'use strict';

    $(".reserved-indicator").each(window.nuget.setPopovers);
    $(".package-warning--vulnerable").each(window.nuget.setPopovers);
    $(".package-warning--deprecated").each(window.nuget.setPopovers);

    const storage = window['localStorage'];
    const focusResultsColumnKey = 'focus_results_column';

    if (storage && storage.getItem(focusResultsColumnKey)) {
        storage.removeItem(focusResultsColumnKey);
        document.getElementById('results-column').focus({ preventScroll: true });
    }

    const searchForm = document.forms.search;
    const allFrameworks = document.querySelectorAll('.framework');
    const allTfms = document.querySelectorAll('.tfm');

    // Checkbox logic for Framework and Tfm filters
    for (const framework of allFrameworks) {
        framework.addEventListener('click', clickFrameworkCheckbox);
    }

    for (const tfm of allTfms) {
        tfm.addEventListener('click', clickTfmCheckbox);
    }

    function clickFrameworkCheckbox() {
        this.indeterminate = false;

        const tfms = document.querySelectorAll('[parent=' + this.id + ']');
        tfms.forEach((tfm) => {
            tfm.checked = false;
        });
    }

    function clickTfmCheckbox() {
        const framework = document.getElementById(this.getAttribute('parent'));
        const tfms = document.querySelectorAll('[parent=' + this.getAttribute('parent') + ']');

        const checkedCount = Array.from(tfms).reduce((accumulator, tfm) => accumulator + (tfm.checked ? 1 : 0), 0);

        framework.checked = false;
        framework.indeterminate = checkedCount !== 0;
    }

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
            this.setAttribute("aria-expanded", "true");

            dataTab.style.display = 'block';
            dataTab.style.maxHeight = dataTab.scrollHeight + "px";
        }
        else {
            this.setAttribute("aria-expanded", "false");

            dataTab.style.display = 'none';
            dataTab.style.maxHeight = 0;
        }
    }

    // Update query params before submitting the form
    function submitSearchForm() {
        constructFilterParameter(searchForm.frameworks, allFrameworks);
        constructFilterParameter(searchForm.tfms, allTfms);

        if (storage) {
            storage.setItem(focusResultsColumnKey, true);
        }

        searchForm.submit();
    }

    // Update the query string with the selected frameworks and tfms
    function constructFilterParameter(searchField, checkboxList) {
        searchField.value = "";

        checkboxList.forEach((framework) => {
            if (framework.checked) {
                searchField.value += framework.id + ",";
            }
        });

        // trim trailing commas
        searchField.value = searchField.value.replace(/,+$/, '');
    }

    // Initialize state for Framework and Tfm checkboxes
    // NOTE: We first click on all selected Framework checkboxes and then on the selected Tfm checkboxes, which
    // allows us to correctly handle cases where a Framework AND one of its child Tfms is present in the query
    function initializeFrameworkAndTfmCheckboxes() {
        var inputFrameworkFilters = searchForm.frameworks.value.split(',').map(f => f.trim()).filter(f => f);
        var inputTfmFilters = searchForm.tfms.value.split(',').map(f => f.trim()).filter(f => f);
        searchForm.frameworks.value = "";
        searchForm.tfms.value = "";

        inputFrameworkFilters.map(id => document.getElementById(id)).forEach(checkbox => checkbox.click());
        inputTfmFilters.map(id => document.getElementById(id)).forEach(checkbox => checkbox.click());

        // expand TFM section if a TFM from that generation has been selected
        allFrameworks.forEach((checkbox) => {
            if (checkbox.indeterminate) {
                document.querySelector('[tab=' + checkbox.id + ']').click();
            }
        });
    }

    // The /profiles pages use this js file too, but some code needs to be applied only to the search page
    if (searchForm) {
        searchForm.sortby.addEventListener('change', (e) => {
            searchForm.sortby.value = e.target.value;
            submitSearchForm();
        });
        searchForm.addEventListener('submit', submitSearchForm);
        initializeFrameworkAndTfmCheckboxes();
    }
});
