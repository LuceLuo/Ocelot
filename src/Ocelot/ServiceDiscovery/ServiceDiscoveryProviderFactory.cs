using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using Ocelot.Configuration;
using Ocelot.Logging;
using Ocelot.ServiceDiscovery.Configuration;
using Ocelot.ServiceDiscovery.Providers;
using Ocelot.Values;

namespace Ocelot.ServiceDiscovery
{
    public class ServiceDiscoveryProviderFactory : IServiceDiscoveryProviderFactory
    {
        private readonly IOcelotLoggerFactory _factory;
        private readonly IMemoryCache _memoryCache;

        public ServiceDiscoveryProviderFactory(IOcelotLoggerFactory factory, IMemoryCache memoryCache)
        {
            _factory = factory;
            _memoryCache = memoryCache;
        }

        public IServiceDiscoveryProvider Get(ServiceProviderConfiguration serviceConfig, DownstreamReRoute reRoute)
        {
            if (reRoute.UseServiceDiscovery)
            {
                return GetServiceDiscoveryProvider(serviceConfig, reRoute.ServiceName);
            }

            var services = new List<Service>();

            foreach (var downstreamAddress in reRoute.DownstreamAddresses)
            {
                var service = new Service(reRoute.ServiceName, new ServiceHostAndPort(downstreamAddress.Host, downstreamAddress.Port), string.Empty, string.Empty, new string[0]);

                services.Add(service);
            }

            return new ConfigurationServiceProvider(services);
        }

        private IServiceDiscoveryProvider GetServiceDiscoveryProvider(ServiceProviderConfiguration serviceConfig, string serviceName)
        {
            if (serviceConfig.Type == "ServiceFabric")
            {
                if (serviceName.Contains(":"))
                {
                    var config = new ServiceFabricConfiguration(serviceConfig.Host, serviceConfig.Port, serviceName);
                    return new ServiceFabricTypeServiceDiscoveryProvider(config, _memoryCache);
                }
                else
                {
                    var config = new ServiceFabricConfiguration(serviceConfig.Host, serviceConfig.Port, serviceName);
                    return new ServiceFabricServiceDiscoveryProvider(config);
                }
            }

            var consulRegistryConfiguration = new ConsulRegistryConfiguration(serviceConfig.Host, serviceConfig.Port, serviceName);
            return new ConsulServiceDiscoveryProvider(consulRegistryConfiguration, _factory);
        }
    }
}
