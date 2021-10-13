using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Bum.EventGrid.Subscrioptions.AzureFunctionsIsolatedSample
{
    class Program
    {
        public static async Task Main()
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .Build();

            await host.RunAsync();
        }
    }
}