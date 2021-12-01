/*******************************************************************************
* Copyright (c) 2020, 2021 Robert Bosch GmbH
* Author: Constantin Ziesche (constantin.ziesche@bosch.com)
*
* This program and the accompanying materials are made available under the
* terms of the Eclipse Public License 2.0 which is available at
* http://www.eclipse.org/legal/epl-2.0
*
* SPDX-License-Identifier: EPL-2.0
*******************************************************************************/
using Makaretu.Dns;
using NLog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace BaSyx.Discovery.mDNS
{
    public class DiscoveryClient : IDisposable
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private ServiceDiscovery serviceDiscovery;
        private ServiceProfile serviceProfile;
        private bool disposedValue;
        private MulticastService mdns;

        public ushort Port { get; }
        public string ServiceName { get; }
        public string ServiceType { get; }
        public IEnumerable<IPAddress> IPAddresses { get; private set; }

        public DiscoveryClient(string serviceName, ushort port, string serviceType) : 
            this(serviceName, port, serviceType, null)
        { }

        public DiscoveryClient(string serviceName, ushort port, string serviceType, IEnumerable<IPAddress> ipAddresses)
        {
            ServiceName = serviceName;
            ServiceType = serviceType;
            Port = port;
            IPAddresses = ipAddresses ?? MulticastService.GetIPAddresses();

            logger.Info("Creating service profile with" +
               "\tServiceName=" + ServiceName +
               "\tServiceType=" + ServiceType +
               "\tPort=" + Port +
               "\tIPAddresses=" + string.Join(";", IPAddresses));

            serviceProfile = new ServiceProfile(ServiceName, ServiceType, Port, IPAddresses);
        }

        /// <summary>
        /// Add information (key-value-pair) to TXT-record of advertise message
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void AddProperty(string key, string value)
        {
            serviceProfile.AddProperty(key, value);
        }

        public void Start()
        {
            logger.Info("Advertisement starting...");

            Task.Run(() =>
            {
                mdns = new MulticastService();
                serviceDiscovery = new ServiceDiscovery(mdns);
                serviceDiscovery.Advertise(serviceProfile);
                mdns.Start();
            });

            logger.Info("Advertisement started successfully");
        }


        public void Stop()
        {
            logger.Info("Advertisement stopping...");

            serviceDiscovery.Unadvertise(serviceProfile);
            mdns.Stop();

            logger.Info("Advertisement stopped successfully");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {                    
                    serviceDiscovery.Dispose();
                    mdns.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
