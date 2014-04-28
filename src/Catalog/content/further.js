
// further flattens and already flattened JSON-LD document 

var further = function (flattened) {
    var result = [];
    for (var i = 0; i !== flattened.length; i += 1) {
        var subject = flattened[i]['@id'];
        for (var prop in flattened[i]) {
            if (prop !== '@id') {
                var predicate = prop;
                for (var j = 0; j !== flattened[i][prop].length; j += 1) {
                    var object = {};
                    if (flattened[i][prop][j]['@id'] !== undefined) {
                        object = { value: flattened[i][prop][j]['@id'], type: 'uri' };
                    }
                    else {
                        object = { value: flattened[i][prop][j]['@value'], type: 'literal' };
                    }
                    result[result.length] = { subject: subject, predicate: predicate, object: object };
                }
            }
        }
    }
    return result;
}
