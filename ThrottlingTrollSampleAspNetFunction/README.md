# ThrottlingTrollSampleAspNetFunction

This [Azure Functions (.NET Isolated)](https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-dotnet-isolated-overview) project demonstrates all the features of [ThrottlingTroll.AzureFunctionsAspNet](https://www.nuget.org/packages/ThrottlingTroll.AzureFunctionsAspNet).

## How to run locally

As a prerequisite, you will need [Azure Functions Core Tools globally installed](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools).

1. (Optional, if you want to use [RedisCounterStore](https://github.com/ThrottlingTroll/ThrottlingTroll/tree/main/ThrottlingTroll.CounterStores.Redis)) Add `RedisConnectionString` setting to [local.settings.json](https://github.com/ThrottlingTroll/ThrottlingTroll-AzureFunctions-Samples/blob/main/ThrottlingTrollSampleAspNetFunction/local.settings.json) file. For a local Redis server that connection string usually looks like `localhost:6379`. 

2. Type `func start` in your terminal window.
