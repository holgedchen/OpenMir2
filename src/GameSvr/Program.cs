using Microsoft.Extensions.Hosting;
using System.Runtime;
using System.Text;

namespace GameSvr
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            GCSettings.LatencyMode = GCSettings.IsServerGC ? GCLatencyMode.Batch : GCLatencyMode.Interactive;
            var serviceRunner = new AppServer();
            await serviceRunner.RunAsync();
        }
    }
}