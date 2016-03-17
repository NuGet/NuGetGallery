var request = require('request');
var url = require('url');
var github = require('./github');
var blobstore = require('./blobstore');
var ghqueue = require('./queue');
var fs = require('fs');
var credentials = require('./credentials');

process();

function getGithubRepo(value){
	if (!value.ProjectUrl) return false;
	var ghurl = url.parse(value.ProjectUrl);
	if (ghurl.hostname !== 'github.com') return false;
	var pathParts = ghurl.pathname.split('/').filter(notEmpty);
	if (pathParts.length >= 2) return {user:pathParts[0], repo: pathParts[1].replace(".git", "")};
	return false;
}

function notEmpty(value){
	return value != "";
}

function process(){
	ghqueue.pop(function(err, obj, complete){

		if (err){
			console.log(err);
			complete();
			return setTimeout(process, 100);
		}

		if (!obj) {
			console.log("queue is empty, sleeping");
			return setTimeout(process,300000);
		}

		console.log(obj.Id);
		github.getRepo(obj, function(err, data, text){

			if (err || !data){

				if (err === "moved" && data){
					var newRepo = getGithubRepo({ ProjectUrl : data});

					fs.appendFileSync("moved.txt", '"' + obj.Id + '" : {user: "' + newRepo.user + '", repo: "' + newRepo.repo +'"},\r\n' );

					obj.user = newRepo.user;
					obj.repo = newRepo.repo;
					// update the object with the new values for the repo, and requeue it
					ghqueue.push(obj);
					complete();
					return setTimeout(process,1000);
				}
				console.log(err, obj.Id + " : " + obj.user + "/" + obj.repo);
				complete();
				return setTimeout(process,1000);
			}


			blobstore.saveObject(credentials.azureStorageContainer, obj.Id, data, function(err){
				if (err) console.log(obj, err);
				complete();
				//console.log(obj.user, obj.repo, data.meta["x-ratelimit-remaining"]);
				if (parseInt(data.meta["x-ratelimit-remaining"]) < 10){
					var resumeTime = parseInt(data.meta["x-ratelimit-reset"]) * 1000;
					var delta = Math.abs(resumeTime - new Date().getTime());
					console.log("sleeping for " + delta/1000 + " seconds");
					setTimeout(process, delta);
				} else {
					setTimeout(process,1000);
				}
			});
		});
	});
}

function toDate(value){
	return new Date(parseInt(value.replace("/Date(", "").replace(")/", "")));
}
