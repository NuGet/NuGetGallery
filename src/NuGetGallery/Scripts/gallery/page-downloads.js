$(function () {
    'use strict';

    $(document).on('dataLoaded', function () {
        makeOlderVersionsCollapsible();
    });

    var chevronIcon;
    var olderVersionsElement;

    function makeOlderVersionsCollapsible() {
        const listContainer = document.getElementById('win-x86-versions').children[1];   //children[0] is the headline, children[1] is the list of versions
        const listContainerParent = document.getElementById('win-x86-versions');
        olderVersionsElement = document.createElement('div');

        // We want to display 2 versions to the users. Others should be collapsed.
        const allVersions = listContainer.querySelectorAll('li');
        var collapsedVersions = Array.from(allVersions).slice(2);

        // If the first two versions in json file are the same, we only want to show the first.
        // The following code checks if they're the same, and if so, removes the second.
        // It checks if they're the same based on the last word, which is a version identifier.

        var firstElement = listContainer.querySelector('li:first-child');
        var secondElement = listContainer.querySelector('li:nth-child(2)');

        var firstText = firstElement.textContent.trim();
        var secondText = secondElement.textContent.trim();

        var firstVersionLastWord = firstText.split(' ').pop();
        var secondVersionLastWord = secondText.split(' ').pop();

        if (firstVersionLastWord === secondVersionLastWord) {
            listContainer.removeChild(secondElement);
        }

        olderVersionsElement.setAttribute('class', 'older-versions-dropdown');
        olderVersionsElement.innerHTML = 'Older versions ' +
            '<button class="toggle-older-versions-button"' +
            'aria-label="Toggles older versions"' +
            'aria-expanded="false"' +
            'aria-controls="olderVersionsToggleButton"' +
            'tabindex="0"' +
            'id="olderVersionsToggleButton"' +
            'type="button">' +
            '<i class="ms-Icon ms-Icon--ChevronDown"' +
            'id="olderVersionsToggleChevron"></i>' +
            '</button>' +
            '<p> These versions are no longer supported and might have vulnerabilities. </p>';

        const versionsList = document.createElement('ul');

        collapsedVersions.forEach(li => {
            versionsList.appendChild(li);
        });

        olderVersionsElement.appendChild(versionsList);
        listContainerParent.appendChild(olderVersionsElement);

        chevronIcon = document.getElementById('olderVersionsToggleChevron');
        var hideVersionsButton = document.getElementById('olderVersionsToggleButton');
        hideVersionsButton.addEventListener('click', toggleOlderVersions);
    }

    function toggleOlderVersions() {
        chevronIcon.classList.toggle('ms-Icon--ChevronDown');
        chevronIcon.classList.toggle('ms-Icon--ChevronUp');
        olderVersionsElement.classList.toggle('show-list-items');
        if (olderVersionsElement.classList.contains('show-list-items')) {
            chevronIcon.parentNode.setAttribute("aria-expanded", "true");
        }
        else {
            chevronIcon.parentNode.setAttribute("aria-expanded", "false");
        }
    }
});