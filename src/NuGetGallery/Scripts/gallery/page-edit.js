var EditViewManager = new function () {
    var _currVersion;
    var _viewModel;
    var _changedState;
    var _resetFunctions;
    var _submitEditUrl;
    var _cancelEditUrl;

    this.init = function (model, submitEditUrl, cancelEditUrl) {
        _submitEditUrl = submitEditUrl;
        _cancelEditUrl = cancelEditUrl;
        _viewModel = model;
        _changedState = {};
        _resetFunctions = {};
        bindData(_viewModel);

        $('#input-select-version').on('change', function () {
            document.location = $(this).val();
        });

        $('input[type="text"], textarea').on('change', function () {
            $(this).addClass("edited");
            _changedState[$(this).attr('id')] = true;
        });

        $('input[type="text"], textarea').each(function (index) {
            _resetFunctions[$(this).attr('id')] = function (newValue) {
                _changedState[$(this).attr('id')] = false;
                $(this).val(newValue);
            }.bind(this, $(this).val());

            $(this).on('click', function (newValue) {
                $(this).removeClass("edited");
                _changedState[$(this).attr('id')] = false;
                $(this).val(newValue);
            }.bind(this, $(this).val()));
        });
    }

    function submitEditAsync(callback, error) {
        $.ajax({
            url: _submitEditUrl,
            type: 'POST',

            data: new FormData($('#edit-metadata-form')[0]),

            cache: false,
            contentType: false,
            processData: false,

            success: function (model, resultCodeString, fullResponse) {
                if (callback) {
                    callback(model);
                }
            },

            error: handleErrors.bind(this, error)
        });
    }

    function cancelEdit() {
        navigateToPage({ location: _cancelEditUrl });
    }
    
    function navigateToPage(verifyResponse) {
        document.location = verifyResponse.location;
    }

    function displayErrors(errors) {
        if (errors == null || errors.length < 1) {
            return;
        }

        var failureContainer = $("#validation-failure-container");
        var failureListContainer = document.createElement("div");
        $(failureListContainer).attr("id", "validation-failure-list");
        $(failureListContainer).attr("data-bind", "template: { name: 'validation-errors', data: data }");
        failureContainer.append(failureListContainer);
        ko.applyBindings({ data: errors }, failureListContainer);

        failureContainer.removeClass("hidden");
    }

    function handleErrors(errorCallback, model, resultCodeString, fullResponse) {
        bindData(null);

        switch (resultCodeString) {
            case "timeout":
                displayErrors(["The operation timed out. Please try again."])
                break;
            case "abort":
                displayErrors(["The operation was aborted. Please try again."])
                break;
            default:
                displayErrors(model.responseJSON);
                break;
        }

        if ((fullResponse && fullResponse.status >= 500) || (model && model.status >= 500)) {
            displayErrors(["There was a server error."])
        }
        
        if (errorCallback) {
            errorCallback();
        }
    }

    function bindData(model) {
        $("#verify-package-block").remove();
        $("#verify-collapser-container").addClass("hidden");
        if (model == null) {
            return;
        }

        var reportContainerElement = document.createElement("div");
        $(reportContainerElement).attr("id", "verify-package-block");
        $(reportContainerElement).attr("class", "collapse in");
        $(reportContainerElement).attr("aria-expanded", "true");
        $(reportContainerElement).attr("data-bind", "template: { name: 'verify-metadata-template', data: data }");
        $("#verify-package-container").append(reportContainerElement);
        ko.applyBindings({ data: model }, reportContainerElement);

        $('#verify-cancel-button').on('click', function () {
            $('#verify-cancel-button').attr('disabled', 'disabled');
            $('#verify-cancel-button').attr('value', 'Cancelling');
            $('#verify-cancel-button').addClass('.loading');
            $('#verify-submit-button').attr('disabled', 'disabled');
            cancelEdit();
        });

        $('#verify-submit-button').on('click', function () {
            $('#verify-cancel-button').attr('disabled', 'disabled');
            $('#verify-submit-button').attr('disabled', 'disabled');
            $('#verify-submit-button').attr('value', 'Submitting');
            $('#verify-submit-button').addClass('.loading');
            submitEditAsync(navigateToPage);
        });

        $('#iconurl-field').on('change', function () {
            $('#icon-preview').attr('src', $('#iconurl-field').val());
        });

        $("#verify-collapser-container").removeClass("hidden");

        window.nuget.configureExpander(
            "edit-metadata-form-container",
            "ChevronRight",
            "Verify",
            "ChevronDown",
            "Verify");
    }
}