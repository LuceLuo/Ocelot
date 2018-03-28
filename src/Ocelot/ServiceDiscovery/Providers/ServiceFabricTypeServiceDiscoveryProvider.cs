using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Ocelot.ServiceDiscovery.Configuration;
using Ocelot.Values;

namespace Ocelot.ServiceDiscovery.Providers
{
    public class ServiceFabricTypeServiceDiscoveryProvider : IServiceDiscoveryProvider
    {
        private readonly ServiceFabricConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;

        public ServiceFabricTypeServiceDiscoveryProvider(ServiceFabricConfiguration configuration, IMemoryCache memoryCache)
        {
            _configuration = configuration;
            _memoryCache = memoryCache;
        }

        public Task<List<Service>> Get()
        {
            //去服务器取可用的所有服务
            //获取可以用的服务实例地址
            var types = _configuration.ServiceName.Split(':');
            if (types.Length != 2)
            {
                throw new ArgumentException("ServiceName 格式不正确", "ServiceName");
            }

            var services = _memoryCache.Get<List<Service>>(_configuration.ServiceName);
            if (services == null)
            {
                var hosts = GetServiceHostsAsync(types[0], types[1]).GetAwaiter().GetResult().ToArray();
                services = new List<Service>();
                for (int i = 0; i < hosts.Count(); i++)
                {
                    services.Add(new Service(_configuration.ServiceName + ":" + i,
                            hosts[i],
                            "doesnt matter with service fabric",
                            "doesnt matter with service fabric",
                            new List<string>()));
                }

                _memoryCache.Set(_configuration.ServiceName, services, TimeSpan.FromSeconds(5));
            }
            return Task.FromResult(services);
        }


        //public async Task<T> GetServiceAsync<T>(string serviceName)
        //{
        //    //获取可以用的服务实例地址
        //    var types = serviceName.Split(':');
        //    if (types.Length != 2)
        //    {
        //        throw new ArgumentException("ServiceName 格式不正确", "ServiceName");
        //    }

        //    //添加内存缓存，时间10分钟
        //    var cache = ServiceLocator.ServiceProvider.GetService<IMemoryCache>();
        //    var key = $"service-{types[1]}-{types[2]}";
        //    var serivceUris = cache.Get<IEnumerable<Uri>>(key);
        //    if (serivceUris == null || serivceUris.Count() == 0)
        //    {
        //        serivceUris = await GetServiceUriAsync(types[1], types[2]);
        //        cache.Set(key, serivceUris, TimeSpan.FromMinutes(10));
        //    }

        //    //随机
        //    var serivceUri = serivceUris.ToArray()[new Random().Next(0, serivceUris.Count() - 1)];

        //    var proxyFactory = new ServiceProxyFactory((c) =>
        //    {
        //        return new FabricTransportServiceRemotingClientFactory();
        //    });

        //    return proxyFactory.CreateServiceProxy<T>(serivceUri);
        //}

        /// <summary>
        /// 获取服务Uri地址
        /// </summary>
        /// <param name="applicationType"></param>
        /// <param name="serviceType"></param>
        /// <returns></returns>
        private async Task<IEnumerable<ServiceHostAndPort>> GetServiceHostsAsync(string applicationType, string serviceType)
        {
            FabricClient client = new FabricClient();
            var applications = await client.QueryManager.GetApplicationListAsync();
            var hostAndPorts = new List<ServiceHostAndPort>();

            foreach (var application in applications)
            {
                if (applicationType.Equals(application.ApplicationTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    var applicationServices = await client.QueryManager.GetServiceListAsync(application.ApplicationName);

                    foreach (var service in applicationServices)
                    {
                        if (serviceType.Equals(service.ServiceTypeName, StringComparison.OrdinalIgnoreCase) && service.ServiceStatus == System.Fabric.Query.ServiceStatus.Active)
                        {
                            var serviceDescription = await client.ServiceManager.GetServiceDescriptionAsync(service.ServiceName);
                            if (serviceDescription.PartitionSchemeDescription.Scheme == PartitionScheme.Singleton)
                            {
                                var partition = await client.ServiceManager.ResolveServicePartitionAsync(service.ServiceName);

                                var endpoints = partition.Endpoints.Select(p => (string)JObject.Parse(p.Address)["Endpoints"][""]);
                                foreach (var endpoint in endpoints)
                                {
                                    var matches = Regex.Matches(endpoint, @"(\w+):\/\/([^/:]+)((:)(\d*))?");
                                    for (int i = 0; i < matches.Count; i++)
                                    {
                                        if (matches[i].Groups.Count > 3)
                                        {
                                            hostAndPorts.Add(new ServiceHostAndPort(matches[i].Groups[2].Value, int.Parse(matches[i].Groups[5].Value)));
                                        }
                                        else
                                        {
                                            hostAndPorts.Add(new ServiceHostAndPort(matches[i].Groups[2].Value, 80));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return hostAndPorts;
        }
    }
}
