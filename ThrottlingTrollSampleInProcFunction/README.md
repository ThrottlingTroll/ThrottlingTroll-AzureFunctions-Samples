# ThrottlingTrollSampleFunction

This Azure Functions (.NET InProc) project demonstrates all the features of [ThrottlingTroll.AzureFunctions](https://www.nuget.org/packages/ThrottlingTroll.AzureFunctions).

## How to run locally

As a prerequisite, you will need [Azure Functions Core Tools globally installed](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools).

1. (Optional, if you want to use [RedisCounterStore](https://github.com/ThrottlingTroll/ThrottlingTroll/tree/main/ThrottlingTroll.CounterStores.Redis)) Add `RedisConnectionString` setting to [local.settings.json](https://github.com/ThrottlingTroll/ThrottlingTroll-AzureFunctions-Samples/blob/main/ThrottlingTrollSampleFunction/local.settings.json) file. For a local Redis server that connection string usually looks like `localhost:6379`. 

2. Type `func start` in your terminal window.
