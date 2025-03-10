using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using Prometheus.DotNetRuntime;

namespace api_under_test
{
    public class Program
    {

        public static Gauge ConfiguredRetries = Metrics.CreateGauge("polly_retries", "How many");
        public static Gauge ConfiguredTimeout = Metrics.CreateGauge("polly_timeout", "How many milliseconds");
        public static Gauge TcpConnections = Metrics.CreateGauge("tcpconnections", "How many");
        public static Gauge ExecutedRetries = Metrics.CreateGauge("executed_retries", "How many");
        public static Gauge GC0= Metrics.CreateGauge("GC0", "How many");
        public static Gauge GC1= Metrics.CreateGauge("GC1", "How many");
        public static Gauge GC2= Metrics.CreateGauge("GC2", "How many");

        private static Timer _timer;
        private static KestrelMetricServer _kestrelMetricServer;
        public static void Main(string[] args)
        {
            var collector = DotNetRuntimeStatsBuilder.Default().StartCollecting();
            _kestrelMetricServer = new Prometheus.KestrelMetricServer(port: 1234);
            _kestrelMetricServer.Start();
            
            _timer = new Timer(LogNetworkAndGcInfo, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            var hostBuilder = CreateHostBuilder(args);
            var host = hostBuilder.Build();
            using var serviceScope = host.Services.CreateScope();
            var life = serviceScope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>();
            life.ApplicationStopping.Register(() => { 
                _kestrelMetricServer?.Stop();
                Console.WriteLine("Stopped metricserver");
            });

            host.Run();
            
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls("http://*:5555");
                });
        private static void LogNetworkAndGcInfo(object state)
        {
            var tcpStat = IPGlobalProperties.GetIPGlobalProperties().GetTcpIPv4Statistics();
            TcpConnections.Set(tcpStat.CurrentConnections);
            GC0.Set(GC.CollectionCount(0));
            GC1.Set(GC.CollectionCount(1));
            GC2.Set(GC.CollectionCount(2));
        }
    }
}
