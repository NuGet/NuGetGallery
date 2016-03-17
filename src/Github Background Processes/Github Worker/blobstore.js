var azure = require('azure-storage');
var credentials = require('./credentials');

var blobService = azure.createBlobService(credentials.azureStorageAccountName, credentials.azureStorageAccountKey);

function createContainer(containerName){
    blobService.createContainerIfNotExists(containerName, function(error){
        if (error) console.log(error);
    });
}

createContainer(credentials.azureStorageContainer);

module.exports.saveObject = function(container, filename, content, cb){
    blobService.createBlockBlobFromText(container, filename, JSON.stringify(content), cb);
}

module.exports.readObject = function(container, filename, cb){
    blobService.getBlobToText(container, filename, function(error, data){
        cb(error, JSON.parse(data));
    });
}
