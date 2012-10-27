var AsyncFileUploadManager = new function () {
    var _isWebkitBrowser = $.browser.webkit;
    var _iframeId = '__fileUploadFrame';
    var _pollingInterval = 200;
    var _pingUrl;
    
    this.init = function (pingUrl, formId) {
        _pingUrl = pingUrl;

        // attach the sumbit event to the form
        $('#' + formId).submit(function () {
            submitForm(this);
            return false;
        });

        if (_isWebkitBrowser) {
            constructIframe();
        }
    }

    function submitForm(form) {
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
            setProgressIndicator(0, 0, null);

            if (_isWebkitBrowser) {
                document.getElementById(_iframeId).contentWindow.Start(_pingUrl, setProgressIndicator);
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
        
        setProgressIndicator(percent, result.FileName);
        if (percent < 100) {
            setTimeout(getProgress, _pollingInterval);
        }
    }

    function onGetProgressError(result) {
        // TODO: what to do with error?
    }

    function setProgressIndicator(percentComplete, fileName) {
        $('#asyncUploadPanel').show();

        percentComplete = Math.min(percentComplete, 100);
        $('#asyncUploadProgressAdvance').width(percentComplete + '%');
        if (fileName) {
            $('#asyncUploadFileName').html('Uploading ' + fileName + '...');
        }
    }

    function constructIframe() {
        var iframe = document.getElementById(_iframeId);
        if (iframe) {
            return;
        }

        iframe = document.createElement('iframe');
        iframe.setAttribute('id', _iframeId);
        iframe.setAttribute('style', 'display: none; visibility: hidden;');

        $(iframe).load(function () {
            var scriptRef = document.createElement('script');
            scriptRef.setAttribute("src", "http://ajax.aspnetcdn.com/ajax/jquery/jquery-1.4.4.min.js");
            scriptRef.setAttribute("type", "text/javascript");
            iframe.contentDocument.body.appendChild(scriptRef);

            var scriptContent = document.createElement('script');
            scriptContent.setAttribute("type", "text/javascript");
            scriptContent.innerHTML = "var _pollingInterval=200;var _callback;var _key;var _pingUrl;function Start(b,c){_callback=c;_pingUrl=b;setTimeout(getProgress,_pollingInterval)}function getProgress(){$.ajax({type:'GET',dataType:'json',url:_pingUrl,success:onGetProgressSuccess,error:onGetProgressError})}function onGetProgressSuccess(a){if(!a){return}var b=a.Progress;var d=a.FileName;_callback(b,d);if(b<100){setTimeout(getProgress,_pollingInterval)}}function onGetProgressError(a){}";
            iframe.contentDocument.body.appendChild(scriptContent);
        });

        document.body.appendChild(iframe);
    }
}