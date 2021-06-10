using Fleck;
using InputManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EliteAPI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using EliteAPI.Abstractions;

namespace elite_hud_server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Create a host that uses EliteAPI
            IHost host = Host.CreateDefaultBuilder()
                 .ConfigureServices((context, service) =>
                 {
                     service.AddEliteAPI(config =>
                     {
                         config.AddEventModule<EliteHUDSocketServer>();
                     });
                     //service.AddTransient<MyAppService>(); // Example
                 })
                 .Build();

            // Get the EliteDangerousAPI api object
            var api = host.Services.GetService<IEliteDangerousApi>();

            // Start the api
            await api.StartAsync();

            await Task.Delay(-1);
        }
    }
}
