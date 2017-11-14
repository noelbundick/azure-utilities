using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using System.Configuration;
using Microsoft.Azure.Management.Compute.Fluent;
using System.IO;
using System.Text;
using Microsoft.Azure.Management.Compute.Fluent.Models;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Functions
{
    public class StartVirtualMachine
    {
        public async Task<IActionResult> RunAsync(HttpRequest req, TraceWriter log)
        {
            var azure = GetAzure();
            var vmName = await GetNameAsync(req, "vm");
            if (vmName == null)
                return new BadRequestObjectResult("Please pass a virtual machine name on the query string or in the request body");

            var username = await GetNameAsync(req, "username");
            var redirectToLogin = false;
            if (!string.IsNullOrWhiteSpace(username))
                redirectToLogin = true;
            
            var vms = await azure.VirtualMachines.ListAsync();
            var targetVm = vms.FirstOrDefault(vm => vm.Name == vmName);

            var startTask = (targetVm.PowerState == PowerState.Running)
                ? Task.CompletedTask
                : targetVm.StartAsync();

            if (redirectToLogin)
            {
                var fqdn = targetVm.GetPrimaryPublicIPAddress().Fqdn;
                await startTask;

                if (targetVm.OSType == OperatingSystemTypes.Linux)
                {
                    // Hacky redirect to ssh protocol for Linux
                    // Can't just use HTTP redirect because .NET will always treat the Location header as a Uri and add a trailing slash
                    var redirectLocation = $"ssh://{username}@{fqdn}";
                    var content = $"<html><head><meta http-equiv=\"refresh\" content=\"0;URL={redirectLocation}\"></head><body><a href=\"{redirectLocation}\">{redirectLocation}</a></body></html>";
                    return new OkObjectResult(content);
                }
                else if (targetVm.OSType == OperatingSystemTypes.Windows)
                {
                    // Download an .rdp file for Windows
                    var rdpFileContents = $"full address:s:{fqdn}:3389\r\nprompt for credentials:i:1\r\nadministrative session:i:1\r\nusername: s:{username}";
                    var content = new StringContent(rdpFileContents);
                    content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                    {
                        FileName = $"{vmName}.rdp"
                    };
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/rdp")
                    {
                        CharSet = "utf-8"
                    };
                    
                    return new OkObjectResult(content);
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

        private static async Task<string> GetNameAsync(HttpRequest req, string key)
        {
            // parse query parameter
            var name = req.Query[key].FirstOrDefault();
            if (name != null)
                return name;

            // If key isn't present there, look inside request body
            using (var reader = new StreamReader(req.Body, Encoding.UTF8))
            {
                var json = await reader.ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(json);
                return data?.name;
            }
        }

        private static IAzure GetAzure()
        {
            var tenantId = ConfigurationManager.AppSettings["tenantId"];
            var sp = new ServicePrincipalLoginInformation
            {
                ClientId = ConfigurationManager.AppSettings["clientId"],
                ClientSecret = ConfigurationManager.AppSettings["clientSecret"]
            };
            return Azure.Authenticate(new AzureCredentials(sp, tenantId, AzureEnvironment.AzureGlobalCloud)).WithDefaultSubscription();
        }
    }
}
