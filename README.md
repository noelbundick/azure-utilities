# azure-utilities

This is a home for a few Azure-specific utilities that I find useful

## StartVirtualMachine

Starts a virtual machine by name

* Runs as an Azure Function
* Starts any VM in your Azure subscription by name
* Immediately provides connection information
  * Linux: redirects and/or provides a link to `ssh://<username>@<FQDN>`
  * Windows: downloads an `.rdp` file to connect
* Only has access to the permissions you grant to its Service Principal

## StartVSTSBuildAgent / StopVSTSBuildAgent

Creates or destroys an Azure Container Instance that runs the VSTS Build Agent

* Dockerfile is in the `vsts-build-agent` folder
* Creates Container Groups in the `vsts` Resource Group
* You'll need to provide AppSettings in your Function App for `VSTS_AGENT_INPUT_URL` and `VSTS_AGENT_INPUT_TOKEN`
* Places VSTS agents in a pool named `AzureContainerInstance`
* Uses the same Service Principal as the other Functions to interact with your Azure Subscription

### Setup

```bash
# Create a Service Principal
az ad sp create-for-rbac -n utility-functions
```

Update `Resources/azuredeploy.parameters.json` with your values
* Use the `appId` value for `servicePrincipalClientId`
* Use the `password` value for `servicePrincipalClientSecret`

```json
...
    "name": {
      "value": "my-utility-functions"
    },
    "servicePrincipalTenantId": {
      "value": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
    },
    "servicePrincipalClientId": {
      "value": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
    },
    "servicePrincipalClientSecret": {
      "value": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
    }
  }
...
```

### Create a function app in Azure

```bash
# Create a resource group
az group create -n utility-functions -l westus2

# Deploy the ARM template to create a function app
az group deployment create -g utility-functions --template-file Resources/azuredeploy.json --parameters @Resources/azuredeploy.parameters.json
```

> <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fnoelbundick%2Fazure-utilities%2Fmaster%2FResources%2Fazuredeploy.json">Click here</a> if you just want to deploy the template in a hurry

### Publish the function to Azure

* Open `azure-utilities.sln` in VS2017
* Right-click the `Functions` project
* Hit `Publish` and select your your newly created function app

> The [azure-functions-core-tools](https://github.com/Azure/azure-functions-cli) on Linux were very new and weren't working reliably when I wrote this - if it's been a while, I'd try that first instead of full Visual Studio