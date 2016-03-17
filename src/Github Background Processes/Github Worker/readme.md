# Github Worker

Node.js application which reads JSON objects from an Azure Queue, interrogates the Github API to retrieve
repository information and readme file (rendered as HTML). Saves this as a blob in an Azure storage.

The package Id is used as the blob name.

## Installation

Install node

Install the application's packages:

```
$ npm install
```

Update the `credentials.js` file with your credentials:

```
module.exports = {
    azureStorageAccountName: "SET_THIS",
    azureStorageAccountKey: "SET_THIS",
    azureStorageContainer: "github",
    azureQueueName: "github",
    githubUsername:"SET_THIS",
    githubPassword:"SET_THIS"
};

```

## Running the Worker

```
$ node app
```
