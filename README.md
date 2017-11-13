# azure-utilities

This is a home for a few Azure-specific utilities that I find useful

### StartVirtualMachine

Starts a virtual machine by name

Features:
* Runs as an Azure Function
* Starts any VM in your Azure subscription by name
* Immediately provides connection information
  * Linux: redirects and/or provides a link to `ssh://<username>@<FQDN>`
  * Windows: downloads an .rdp file to connect
* Only has access to the permissions you grant to its Service Principal