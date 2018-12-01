using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using System.Net.Http;
using System.Threading;
using System.Net.Http.Headers;
using System.Linq;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.Compute.Fluent.Models;

namespace Acanthamoeba.Functions
{
    public static class StartVM
    {
        private static IAzure _azure = GetAzure();

        [FunctionName("StartVM")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("StartVM invoked");

            var vmName = req.Query["vm"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(vmName))
                return new BadRequestObjectResult("You must pass a virtual machine name in the query string. Ex: ?vm=myVirtualMachine");
            
            var username = req.Query["username"].FirstOrDefault();
            var redirectToLogin = !string.IsNullOrWhiteSpace(username);
            
            var vms = await _azure.VirtualMachines.ListAsync();
            var targetVm = vms.FirstOrDefault(vm => vm.Name == vmName);

            var startTask = (targetVm.PowerState == PowerState.Running)
                ? Task.CompletedTask
                : targetVm.StartAsync();

            if (redirectToLogin)
            {
                var ipAddress = targetVm.GetPrimaryPublicIPAddress();
                var endpoint = string.IsNullOrWhiteSpace(ipAddress.Fqdn) ? ipAddress.IPAddress : ipAddress.Fqdn;
                await startTask;

                if (targetVm.OSType == OperatingSystemTypes.Linux)
                {
                    // Hacky redirect to ssh protocol for Linux
                    // Can't just use HTTP redirect because .NET will always treat the Location header as a Uri and add a trailing slash
                    var redirectLocation = $"ssh://{username}@{endpoint}";
                    var content = $"<html><head><meta http-equiv=\"refresh\" content=\"0;URL={redirectLocation}\"></head><body><a href=\"{redirectLocation}\">{redirectLocation}</a></body></html>";

                    var result = new ContentResult();
                    result.Content = content;
                    result.ContentType = "text/html";
                    return result;
                }
                else if (targetVm.OSType == OperatingSystemTypes.Windows)
                {
                    // Download an .rdp file for Windows
                    var rdpFileContents = $"full address:s:{endpoint}:3389\r\nprompt for credentials:i:1\r\nadministrative session:i:1\r\nusername: s:{username}";

                    var result = new FileContentResult(System.Text.Encoding.UTF8.GetBytes(rdpFileContents), "application/rdp");
                    result.FileDownloadName = $"{vmName}.rdp";
                    return result;
                }
                else
                {
                    return new OkObjectResult($"{vmName} is running");
                }
            }
            else
            {
                await startTask;
                return new OkObjectResult($"{vmName} is running");
            }
        }

        private static IAzure GetAzure()
        {
            var tokenProvider = new AzureServiceTokenProvider();
            var appAuthCreds = new AppAuthenticationAzureCredentials(tokenProvider, AzureEnvironment.AzureGlobalCloud);
            return Azure.Authenticate(appAuthCreds).WithDefaultSubscription();
        }
    }
}
