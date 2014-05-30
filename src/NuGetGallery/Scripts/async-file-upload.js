var AsyncFileUploadManager = new function () {
    var _isWebkitBrowser = false; // $.browser.webkit is not longer supported on jQuery
    var _iframeId = '__fileUploadFrame';
    var _pollingInterval = 200;
    var _pingUrl;
    var _failureCount;
    var _isUploadInProgress;

    this.init = function (pingUrl, formId, jQueryUrl) {
        _pingUrl = pingUrl;

        // attach the sumbit event to the form
        $('#' + formId).submit(function () {
            $('#' + formId).find(':submit').attr('disabled', 'disabled');
            $('#' + formId).find(':submit').val('Uploading...');
            submitForm(this);
            return false;
        });

        if (_isWebkitBrowser) {
            constructIframe(jQueryUrl);
        }
    }

    function submitForm(form) {
        if (_isUploadInProgress) {
            return;
        }

        if (!form.action) {
            form.submit();
            return;
        }

        // count the number of file fields which have selected files
        var totalFile = 0;
        $('input[type=file]', form).each(function (index, el) { if (el.value) totalFile++; });

        form.submit();

        // only show progress indicator if the form actually uploads some files
        if (totalFile > 0) {
            _isUploadInProgress = true;
            _failureCount = 0;

            setProgressIndicator(0, '');

            if (_isWebkitBrowser) {
                document.getElementById(_iframeId).contentWindow.start(_pingUrl, setProgressIndicator, onGetProgressError);
            }
            else {
                setTimeout(getProgress, 100);
            }
        }
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
        $('#asyncUploadPanel').show();

        percentComplete = Math.min(percentComplete, 100);
        $('#asyncUploadProgressAdvance').width(percentComplete + '%');

        var status;
        if (percentComplete == 0) {
            status = 'Start uploading...';
        }
        else {
            status = 'Uploading ' + fileName + '...' + percentComplete + '%';
        }

        $('#asyncUploadFileName').html(status);
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