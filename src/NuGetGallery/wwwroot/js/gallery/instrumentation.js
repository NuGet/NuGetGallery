window["initializeNuGetInstrumentation"] = function (config) {
    var instrumentation = {
        initialize: function () {
            console.log("NuGet instrumentation shim: initialized %o", config);
        },
        trackMetric: function (metric, customProperties) {
            console.log("NuGet instrumentation shim: metric %o %o", metric, customProperties);
        }
    };
    instrumentation.initialize();
    return instrumentation;
};
