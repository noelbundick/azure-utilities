# azure-utilities

This is a home for a few Azure-specific utilities that I find useful

## Acanthamoeba.Functions

Azure Functions to do some common tasks

Make sure you set the `AzureServicesAuthConnectionString` AppSetting to a [valid connection string](https://docs.microsoft.com/en-us/azure/key-vault/service-to-service-authentication#connection-string-support). Ex: to use User Assigned Identity, use something like `RunAs=App;AppId=2bab7452-535d-4beb-9124-5e8976959842;`

### StartVM

Starts a virtual machine by name

* Runs as an Azure Function
* Starts any VM in your Azure subscription by name
* Immediately provides connection information
  * Linux: redirects and/or provides a link to `ssh://<username>@<FQDN>`
  * Windows: downloads an `.rdp` file to connect
* Only has access to the permissions you grant to its Service Principal