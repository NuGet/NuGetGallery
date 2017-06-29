var EditViewManager = new function () {
    var _currVersion;
    var _viewModel;
    var _changedState;
    var _resetFunctions;

    this.init = function (model) {
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

    function bindData(model) {
        $("#verify-package-block").remove();
        if (model === null) {
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
            $('#input-select-file').val("");
            $('#file-select-feedback').attr('placeholder', 'Browse to select a package file...');
            // Whether the cancel fails or not, we want to upload the next one.
            cancelUploadAsync();
        });

        $('#verify-submit-button').on('click', function () {
            $('#verify-cancel-button').attr('disabled', 'disabled');
            $('#verify-submit-button').attr('disabled', 'disabled');
            $('#verify-submit-button').attr('value', 'Submitting');
            $('#verify-submit-button').addClass('.loading');
            submitVerifyAsync();
        });

        $('#iconurl-field').on('change', function () {
            $('#icon-preview').attr('src', $('#iconurl-field').val());
        })

        window.nuget.configureExpander(
            "verify-package-form",
            "ChevronRight",
            "Verify",
            "ChevronDown",
            "Verify");
    }
}