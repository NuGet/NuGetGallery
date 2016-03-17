# Package Crawler

Indexes through the Nuget API (v3) searching for packages with a Github URL. Adds
JSON objects to an Azure Queue for those that do.

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
    azureQueueName: "github"
};
```

## Running the Crawler

```
$ node app
```
