var azure = require('azure-storage');
var credentials = require('./credentials');
var queueName = credentials.azureQueueName;
var queueService = azure.createQueueService(credentials.azureStorageAccountName, credentials.azureStorageAccountKey);

function createQueue(queueName){
    queueService.createQueueIfNotExists(queueName, function(error){
        if (error){
            console.log(error);
        }
    });
}

createQueue(queueName)

module.exports.push = function(content){
    queueService.createMessage(queueName, JSON.stringify(content), function(err){
        if (err) console.log(err);
    });
}

module.exports.pop = function(cb){
    queueService.getMessages(queueName, function(err, messages){
    	if (messages && messages.length){
            var err = null;
            try {
            var obj = JSON.parse(messages[0].messagetext);
            } catch (ex){
                console.log(messages[0]);
                err = ex;
            }
    		return cb(err, obj, function(){
    			queueService.deleteMessage(queueName, messages[0].messageid, messages[0].popreceipt, function(error) {
    				if (error) console.log(error);
    			});
    		});
    	}
    	cb(err);
    });
}
