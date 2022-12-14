var HistoryViewModel = (function () {
    'use strict';

    return function () {
        var $self = this;

        this.issue = ko.observable();
        this.historyEntries = ko.observableArray();
    };
}());

var EditViewModel = (function () {
    'use strict';

    return function (editUrl) {
        var $self = this;

        this.issue = ko.observable();
        this.editAssignedToId = ko.observable();
        this.editIssueStatusId = ko.observable();
        this.editIssueComment = ko.observable();
        this.assignedToChoices = ko.observableArray();
        this.issueStatusChoices = ko.observableArray();

        this.updateSupportRequest = function (success, error) {
            var model = {
                issueKey: $self.issue().Key,
                assignedToId: $self.editAssignedToId(),
                issueStatusId: $self.editIssueStatusId(),
                comment: $self.editIssueComment()
            };

            $.ajax({
                url: editUrl,
                type: 'POST',
                cache: false,
                dataType: 'json',
                data: window.nuget.addAjaxAntiForgeryToken(model),
                success: success
            })
            .fail(error);
        };
    };
}());

var SupportRequestsViewModel = (function () {
    'use strict';

    return function (editUrl, filterUrl, historyUrl) {
        var $self = this;

        this.editUrl = editUrl;
        this.filterUrl = filterUrl;
        this.historyUrl = historyUrl;
        this.editSupportRequestForm = $('#editSupportRequest-form').get(0);
        this.editAssignedToCtrl = $('#editAssignedTo').get(0);
        this.editIssueStatusCtrl = $('#editIssueStatus').get(0);
        this.editIssueCommentCtrl = $('#editIssueComment').get(0);
        this.editViewModel = new EditViewModel(editUrl);

        this.historyTableCtrl = $('#history-table').get(0);
        this.historyViewModel = new HistoryViewModel();

        this.assignedToFilter = ko.observable();
        this.issueStatusIdFilter = ko.observable();
        this.reasonFilter = ko.observable();
        this.pageNumber = ko.observable(1);
        this.maxPageNumber = ko.observable(1);
        this.take = ko.observable(30);

        this.hasPreviousPage = ko.computed(function () {
            return $self.pageNumber() > 1;
        });

        this.hasNextPage = ko.computed(function () {
            return $self.pageNumber() < $self.maxPageNumber();
        });

        this.goToPreviousPage = function () {
            $self.filter($self.pageNumber() - 1, $self.take());
        };
        this.goToNextPage = function () {
            $self.filter($self.pageNumber() + 1, $self.take());
        };

        this.filteredIssues = ko.observableArray();
        this.assignedToChoices = ko.observableArray();
        this.issueStatusChoices = ko.observableArray();
        this.reasonChoices = ko.observableArray();

        this.styleButtons = function () {
            $('a.editButton').button(
                {
                    icons: {
                        primary: 'ui-icon-pencil'
                    }
                });
            $('a.historyButton').button(
                {
                    icons: {
                        primary: 'ui-icon-clock'
                    }
                });
            $('a.contactButton').button(
                {
                    icons: {
                        primary: 'ui-icon-mail-closed'
                    }
                });
        };

        this.updateSupportRequest = function () {
            $self.editViewModel.updateSupportRequest(
                function () {
                    $self.pageNumber(0);
                    $self.filter();
                    $self.editSupportRequestDialog.dialog("close");
                },
                function (jqXhr, textStatus, errorThrown) {
                    alert("Error: " + errorThrown);
                });
        };

        this.historyDialog = $('#history-dialog').dialog({
            autoOpen: false,
            modal: true,
            width: 800,
            overlay: {
                backgroundColor: '#000',
                opacity: 0.5
            },
            buttons: {
                "Close": function () {
                    $self.historyDialog.dialog("close");
                }
            }
        });

        this.editSupportRequestFields = $([])
            .add($self.editAssignedToCtrl)
            .add($self.editIssueStatusCtrl)
            .add($self.editIssueCommentCtrl);

        this.editSupportRequestDialog = $("#editSupportRequest-dialog").dialog({
            autoOpen: false,
            modal: true,
            width: 400,
            overlay: {
                backgroundColor: '#000',
                opacity: 0.5
            },
            buttons: {
                "Save Changes": $self.updateSupportRequest,
                Cancel: function () {
                    $self.editSupportRequestDialog.dialog("close");
                }
            },
            close: function () {
                $('#editSupportRequest-form')[0].reset();
                $self.editSupportRequestFields.removeClass("ui-state-error");
            }
        });

        this.editSupportRequest = function (supportRequestViewModel) {
            
            $self.editViewModel.issue(supportRequestViewModel);
            $self.editViewModel.assignedToChoices($self.assignedToChoices());
            $self.editViewModel.issueStatusChoices($self.issueStatusChoices.filter(function (value) {
                return value.Text !== 'Unresolved';
            })());

            $self.editViewModel.editAssignedToId(supportRequestViewModel.AssignedTo);
            $self.editViewModel.editIssueStatusId(supportRequestViewModel.IssueStatusId);

            $self.editSupportRequestDialog.dialog('option', 'title', 'Edit SR-' + supportRequestViewModel.Key);
            $self.editSupportRequestDialog.dialog('open');
            return false;
        };

        this.generateContactUserUrl = function (supportRequestViewModel) {
            return 'mailto:' + supportRequestViewModel.OwnerEmail
                + '?subject=[NuGet.org Support] ' + supportRequestViewModel.IssueTitle
                + '&CC=support@nuget.org';
        };

        this.showHistory = function (supportRequestViewModel) {

            var url = $self.generateHistoryUrl(supportRequestViewModel);

            $.ajax({
                url: url,
                type: 'GET',
                cache: false,
                dataType: 'json',
                success: function (data) {
                    
                    $self.historyViewModel.issue(supportRequestViewModel);
                    $self.historyViewModel.historyEntries(data);

                    $self.historyDialog.dialog('option', 'title', 'History for SR-' + supportRequestViewModel.Key);
                    $self.historyDialog.dialog('open');
                }
            })
                .fail(function (jqXhr, textStatus, errorThrown) {
                    alert("Error: " + errorThrown);
                });

            return false;
        };

        this.generateUserProfileUrl = function (supportRequestViewModel) {
            if (supportRequestViewModel.CreatedBy.toUpperCase !== 'ANONYMOUS') {
                return '/Profiles/' + supportRequestViewModel.CreatedBy;
            }
            return '#';
        };

        this.generatePackageDetailsUrl = function (supportRequestViewModel) {
            return '/packages/' + supportRequestViewModel.PackageId + '/' + supportRequestViewModel.PackageVersion;
        };

        this.generateHistoryUrl = function (supportRequestViewModel) {
            return $self.historyUrl + '?id=' + supportRequestViewModel.Key;
        };

        this.getStyleForIssueStatus = function (supportRequestViewModel) {
            if (supportRequestViewModel.IssueStatusName.toUpperCase() === 'NEW') {
                return 'color: #FF1F19; style: bold;';
            }
            else if (supportRequestViewModel.IssueStatusName.toUpperCase() === 'RESOLVED') {
                return 'color: #09B25B; style: bold;';
            }
            else {
                return 'color: #FF8D00; style: bold;';
            }
        };

        this.applyFilter = function () {
            $self.filter($self.pageNumber(), $self.take());
        };

        this.filter = function (pageNumber, take) {

            var url = $self.filterUrl + '?pageNumber=' + pageNumber + '&take=' + take;

            if ($self.assignedToFilter() !== undefined) {
                url += '&assignedToId=' + $self.assignedToFilter();
            }

            if ($self.reasonFilter() !== undefined && $self.reasonFilter() !== '') {
                url += '&reason=' + $self.reasonFilter();
            }

            if ($self.issueStatusIdFilter() !== undefined) {
                url += '&issueStatusId=' + $self.issueStatusIdFilter();
            }

            $.ajax({
                url: url,
                type: 'GET',
                cache: false,
                dataType: 'json',
                success: function (data) {
                    var parsed = JSON.parse(data);
                    $self.filteredIssues(parsed.Issues);
                    $self.pageNumber(parsed.CurrentPageNumber);
                    $self.maxPageNumber(parsed.MaxPage);
                    $self.styleButtons();
                }
            })
                .fail(function (jqXhr, textStatus, errorThrown) {
                    alert("Error: " + errorThrown);
                });
        };
    };

}());

(function () {
    'use strict';

    $(function () {
        ko.bindingHandlers.datetime = {
            update: function (element, valueAccessor) {
                var value = valueAccessor();
                var date = moment(value);
                $(element).text(date.format("L") + " " + date.format("LTS"));
            }
        };
    });
}());
