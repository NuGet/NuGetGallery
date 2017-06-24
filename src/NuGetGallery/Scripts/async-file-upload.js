var AsyncFileUploadManager = new function () {
    var _actionUrl;
    var _cancelUrl;
    var _submitVerifyUrl;
    var _isWebkitBrowser = false; // $.browser.webkit is not longer supported on jQuery
    var _iframeId = '__fileUploadFrame';
    var _formId;
    var _pollingInterval = 500;
    var _pingUrl;
    var _failureCount;
    var _isUploadInProgress;

    this.init = function (pingUrl, formId, jQueryUrl, actionUrl, cancelUrl, submitVerifyUrl) {
        _pingUrl = pingUrl;
        _formId = formId;
        _actionUrl = actionUrl;
        _cancelUrl = cancelUrl;
        _submitVerifyUrl = submitVerifyUrl;

        // attach the sumbit event to the form
        $('#' + formId).submit(function () {
            $('#' + formId).find(':submit').attr('disabled', 'disabled');
            $('#' + formId).find(':submit').val('Uploading...');
            submitForm(this);
            return false;
        });

        $('#file-select-feedback').on('click', function () {
            $('#input-select-file').click();
        })

        $('#input-select-file').on('change', function () {
            clearErrors();
            var fileName = $('#input-select-file').val().split("\\").pop();

            if (fileName.length > 0) {
                $('#file-select-feedback').attr('placeholder', fileName);
                // Whether the cancel fails or not, we want to upload the next one.
                cancelUploadAsync(startUploadAsync, startUploadAsync);
            } else {
                $('#file-select-feedback').attr('placeholder', 'Browse to select a package file...');
            }
        })

        if (InProgressPackage != null) {
            bindData(InProgressPackage);
        }

        if (_isWebkitBrowser) {
            constructIframe(jQueryUrl);
        }
    }

    function startUploadAsync(callback, error) {
        // count the number of file fields which have selected files
        var totalFile = 0;
        $('input[type=file]').each(function (index, el) { if (el.value) totalFile++; });
        if (totalFile < 1) {
            return;
        }

        startProgressBar();

        $.ajax({
            url: _actionUrl,
            type: 'POST',

            data: new FormData($('#' + _formId)[0]),

            cache: false,
            contentType: false,
            processData: false,

            success: function (model, resultCodeString, fullResponse) {
                bindData(model);
                endProgressBar();
                if (callback) {
                    callback();
                }

            },

            error: function (model, resultCodeString, fullResponse) {
                bindData(null);
                displayErrors(model.responseJSON);
                endProgressBar();
                if (error) {
                    error();
                }
            }
        });
    }

    function submitVerifyAsync(callback, error) {
        $.ajax({
            url: _submitVerifyUrl,
            type: 'POST',

            data: new FormData($('#verify-metadata-form')[0]),

            cache: false,
            contentType: false,
            processData: false,

            success: function (model, resultCodeString, fullResponse) {
                document.location = model.location;
            },

            error: function (model, resultCodeString, fullResponse) {
                bindData(null);
                displayErrors(model.responseJSON);
                endProgressBar();
                if (error) {
                    error();
                }
            }

        });
    }

    function cancelUploadAsync(callback, error) {
        $('#warning-container').hide();
        $.ajax({
            url: _cancelUrl,
            type: 'POST',

            data: new FormData($('#cancel-form')[0]),

            cache: false,
            contentType: false,
            processData: false,

            success: function (model, resultCodeString, fullResponse) {
                bindData(model);
                if (callback) {
                    callback();
                }
            },

            error: function (model, resultCodeString, fullResponse) {
                bindData(null);
                displayErrors(model.responseJSON);
                endProgressBar();
                if (error) {
                    error();
                }
            }

        });
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

        failureContainer.show();
    }

    function clearErrors() {
        $("#validation-failure-container").hide();
        $("#validation-failure-list").remove();
    }

    function bindData(model) {
        $("#verify-package-block").remove();
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
            // Whether the cancel fails or not, we want to upload the next one.
            cancelUploadAsync();
        });

        $('#verify-submit-button').on('click', function () {
            submitVerifyAsync();
        });

        window.nuget.configureExpander(
            "verify-package-form",
            "ChevronRight",
            "Verify",
            "ChevronDown",
            "Verify");
    }

    function submitForm(form) {
        if (_isUploadInProgress) {
            return;
        }

        if (!form.action) {
            return;
        }

        // count the number of file fields which have selected files
        var totalFile = 0;
        $('input[type=file]', form).each(function (index, el) { if (el.value) totalFile++; });

        // only show progress indicator if the form actually uploads some files
        if (totalFile > 0) {
        }
    }

    function startProgressBar() {
        _isUploadInProgress = true;
        _failureCount = 0;

        setProgressIndicator(0, '');
        $("#upload-progress-bar-container").show();
        setTimeout(getProgress, 100);

        //if (_isWebkitBrowser) {
        //    document.getElementById(_iframeId).contentWindow.start(_pingUrl, setProgressIndicator, onGetProgressError);
        //}
        //else {
        //    setTimeout(getProgress, 100);
        //}
    }

    function endProgressBar() {
        $("#upload-progress-bar-container").hide();
        _isUploadInProgress = false;
    }

    function getProgress() {
        $.ajax({
            type: 'GET',
            dataType: 'json',
            url: _pingUrl,
            success: onGetProgressSuccess,
            error: onGetProgressError
        });
    }

    function onGetProgressSuccess(result) {
        if (!result) {
            return;
        }

        var percent = result.Progress;

        if (!result.FileName) {
            return;
        }

        setProgressIndicator(percent, result.FileName);
        if (percent < 100) {
            setTimeout(getProgress, _pollingInterval);
        }
        else {
            _isUploadInProgress = false;
        }
    }

    function onGetProgressError(result) {
        if (++_failureCount < 3) {
            setTimeout(getProgress, _pollingInterval);
        }
    }

    function setProgressIndicator(percentComplete, fileName) {
        $("#upload-progress-bar").width(percentComplete + "%")
            .attr("aria-valuenow", percentComplete)
            .text(percentComplete + "%");
        //$('#asyncUploadPanel').show();

        //percentComplete = Math.min(percentComplete, 100);
        //$('#asyncUploadProgressAdvance').width(percentComplete + '%');

        //var status;
        //if (percentComplete == 0) {
        //    status = 'Start uploading...';
        //}
        //else {
        //    status = 'Uploading ' + fileName + '...' + percentComplete + '%';
        //}

        //$('#asyncUploadFileName').html(status);
    }

    function constructIframe(jQueryUrl) {
        var iframe = document.getElementById(_iframeId);
        if (iframe) {
            return;
        }

        iframe = document.createElement('iframe');
        iframe.setAttribute('id', _iframeId);
        iframe.setAttribute('style', 'display: none; visibility: hidden;');

        $(iframe).load(function () {
            var scriptRef = document.createElement('script');
            scriptRef.setAttribute("src", jQueryUrl);
            scriptRef.setAttribute("type", "text/javascript");
            iframe.contentDocument.body.appendChild(scriptRef);

            var scriptContent = document.createElement('script');
            scriptContent.setAttribute("type", "text/javascript");
            scriptContent.innerHTML = "var _callback,_error, _key, _pingUrl, _fcount;function start(b,c,e){_callback=c;_pingUrl=b;_error=e;_fcount=0;setTimeout(getProgress,200)}function getProgress(){$.ajax({type:'GET',dataType:'json',url:_pingUrl,success:onSuccess,error:_error})}function onSuccess(a){if(!a){return}var b=a.Progress;var d=a.FileName;if(!d){return}_callback(b,d);if(b<100){setTimeout(getProgress,200)}}function onError(a){if(++_fcount<3){setTimeout(getProgress,200)}}";
            iframe.contentDocument.body.appendChild(scriptContent);
        });

        document.body.appendChild(iframe);
    }
}