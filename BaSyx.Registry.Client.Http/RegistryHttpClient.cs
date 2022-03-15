/*******************************************************************************
* Copyright (c) 2022 Bosch Rexroth AG
* Author: Constantin Ziesche (constantin.ziesche@bosch.com)
*
* This program and the accompanying materials are made available under the
* terms of the MIT License which is available at
* https://github.com/eclipse-basyx/basyx-dotnet/blob/main/LICENSE
*
* SPDX-License-Identifier: MIT
*******************************************************************************/
using BaSyx.API.Interfaces;
using BaSyx.Models.Connectivity;
using BaSyx.Models.AdminShell;
using BaSyx.Utils.Client.Http;
using BaSyx.Utils.DependencyInjection;
using BaSyx.Utils.ResultHandling;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using BaSyx.API.Http;
using BaSyx.Utils.Extensions;

namespace BaSyx.Registry.Client.Http
{
    public class RegistryHttpClient : SimpleHttpClient, IAssetAdministrationShellRegistryInterface
    {
        private static readonly ILogger logger = LoggingExtentions.CreateLogger<RegistryHttpClient>();
        public RegistryClientSettings Settings { get; }

        public const string REGISTRY_BASE_PATH = "api/v1/registry";
        public const string SUBMODEL_PATH = "submodels";
        public const string PATH_SEPERATOR = "/";

        private string baseUrl = null;
        
        private CancellationTokenSource RepeatRegistrationCancellationToken = null;

        public void LoadSettings(RegistryClientSettings settings)
        {
            LoadProxy(settings.ProxyConfig);

            if (settings.ClientConfig.RequestConfig.RequestTimeout.HasValue)
                SetDefaultTimeout(TimeSpan.FromMilliseconds(settings.ClientConfig.RequestConfig.RequestTimeout.Value));

            baseUrl = settings.RegistryConfig.RegistryUrl.TrimEnd('/');
        }

        public RegistryHttpClient() : this(null, null)
        { }
        public RegistryHttpClient(RegistryClientSettings registryClientSettings) : this(registryClientSettings, null)
        { }
        public RegistryHttpClient(RegistryClientSettings registryClientSettings, HttpMessageHandler messageHandler) : base(messageHandler)
        {
            JsonSerializerSettings = new DependencyInjectionJsonSerializerSettings();
            Settings = registryClientSettings ?? RegistryClientSettings.LoadSettings() ?? throw new NullReferenceException("Settings is null");
            LoadSettings(Settings);
        }

        public Uri GetPath(string requestPath, string aasId = null, string submodelId = null)
        {
            string path = baseUrl;

            if (string.IsNullOrEmpty(requestPath))
                return new Uri(path);

            if(!string.IsNullOrEmpty(aasId))
            {
                requestPath = requestPath.Replace("{aasIdentifier}", aasId.Base64UrlEncode());
            }

            if (!string.IsNullOrEmpty(submodelId))
            {
                requestPath = requestPath.Replace("{submodelIdentifier}", submodelId.Base64UrlEncode());
            }

            return new Uri(path + requestPath);
        }

        public void RepeatRegistration(IAssetAdministrationShellDescriptor aasDescriptor, TimeSpan interval, CancellationTokenSource cancellationToken)
        {
            RepeatRegistrationCancellationToken = cancellationToken;
            Task.Factory.StartNew(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    IResult<IAssetAdministrationShellDescriptor> result = UpdateAssetAdministrationShellRegistration(aasDescriptor.Identification.Id, aasDescriptor);
                    logger.LogInformation("Registration-Renewal - Success: " + result.Success + " | Messages: " + result.Messages.ToString());
                    await Task.Delay(interval);
                }
            }, cancellationToken.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        } 
        
        public void CancelRepeatingRegistration()
        {
            RepeatRegistrationCancellationToken?.Cancel();
        }

        public IResult<IAssetAdministrationShellDescriptor> CreateAssetAdministrationShellRegistration(IAssetAdministrationShellDescriptor aasDescriptor)
        {
            if (aasDescriptor == null)
                return new Result<IAssetAdministrationShellDescriptor>(new ArgumentNullException(nameof(aasDescriptor)));

            Uri uri = GetPath(AssetAdministrationShellRegistryRoutes.SHELL_DESCRIPTORS);
            var request = base.CreateJsonContentRequest(uri, HttpMethod.Post, aasDescriptor);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<IAssetAdministrationShellDescriptor>(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult<IAssetAdministrationShellDescriptor> UpdateAssetAdministrationShellRegistration(string aasId, IAssetAdministrationShellDescriptor aasDescriptor)
        {
            if (string.IsNullOrEmpty(aasId))
                return new Result<IAssetAdministrationShellDescriptor>(new ArgumentNullException(nameof(aasId)));
            if (aasDescriptor == null)
                return new Result<IAssetAdministrationShellDescriptor>(new ArgumentNullException(nameof(aasDescriptor)));

            Uri uri = GetPath(AssetAdministrationShellRegistryRoutes.SHELL_DESCRIPTOR_ID, aasId);
            var request = base.CreateJsonContentRequest(uri, HttpMethod.Put, aasDescriptor);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<IAssetAdministrationShellDescriptor>(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult<IAssetAdministrationShellDescriptor> RetrieveAssetAdministrationShellRegistration(string aasId)
        {
            if (string.IsNullOrEmpty(aasId))
                return new Result<IAssetAdministrationShellDescriptor>(new ArgumentNullException(nameof(aasId)));

            Uri uri = GetPath(AssetAdministrationShellRegistryRoutes.SHELL_DESCRIPTOR_ID, aasId);
            var request = base.CreateRequest(uri, HttpMethod.Get);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<IAssetAdministrationShellDescriptor>(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult<IQueryableElementContainer<IAssetAdministrationShellDescriptor>> RetrieveAllAssetAdministrationShellRegistrations(Predicate<IAssetAdministrationShellDescriptor> predicate)
        {
            if (predicate == null)
                return new Result<IQueryableElementContainer<IAssetAdministrationShellDescriptor>>(new ArgumentNullException(nameof(predicate)));

            Uri uri = GetPath(AssetAdministrationShellRegistryRoutes.SHELL_DESCRIPTORS);
            var request = base.CreateRequest(uri, HttpMethod.Get);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<IEnumerable<IAssetAdministrationShellDescriptor>>(response, response.Entity);

            if (!result.Success || result.Entity == null)
            {
                response?.Entity?.Dispose();
                return new Result<IQueryableElementContainer<IAssetAdministrationShellDescriptor>>(result);
            }
            else
            {
                response?.Entity?.Dispose();
                var foundItems = result.Entity.Where(w => predicate.Invoke(w));
                return new Result<IQueryableElementContainer<IAssetAdministrationShellDescriptor>>(result.Success, foundItems?.AsQueryableElementContainer(), result.Messages);
            }
        }

        public IResult<IQueryableElementContainer<IAssetAdministrationShellDescriptor>> RetrieveAllAssetAdministrationShellRegistrations()
        {
            Uri uri = GetPath(AssetAdministrationShellRegistryRoutes.SHELL_DESCRIPTORS);
            var request = base.CreateRequest(uri, HttpMethod.Get);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<IEnumerable<IAssetAdministrationShellDescriptor>>(response, response.Entity);
            response?.Entity?.Dispose();
            return new Result<IQueryableElementContainer<IAssetAdministrationShellDescriptor>>(result.Success, result.Entity?.AsQueryableElementContainer(), result.Messages);
        }

        public IResult DeleteAssetAdministrationShellRegistration(string aasId)
        {
            if (string.IsNullOrEmpty(aasId))
                return new Result(new ArgumentNullException(nameof(aasId)));

            Uri uri = GetPath(AssetAdministrationShellRegistryRoutes.SHELL_DESCRIPTOR_ID, aasId);
            var request = base.CreateRequest(uri, HttpMethod.Delete);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult<ISubmodelDescriptor> CreateSubmodelRegistration(string aasId, ISubmodelDescriptor submodelDescriptor)
        {
            if (string.IsNullOrEmpty(aasId))
                return new Result<ISubmodelDescriptor>(new ArgumentNullException(nameof(aasId)));
            if (submodelDescriptor == null)
                return new Result<ISubmodelDescriptor>(new ArgumentNullException(nameof(submodelDescriptor)));

            Uri uri = GetPath(AssetAdministrationShellRegistryRoutes.SHELL_DESCRIPTOR_ID_SUBMODEL_DESCRIPTORS, aasId);
            var request = base.CreateJsonContentRequest(uri, HttpMethod.Post, submodelDescriptor);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<ISubmodelDescriptor>(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult<ISubmodelDescriptor> UpdateSubmodelRegistration(string aasId, string submodelId, ISubmodelDescriptor submodelDescriptor)
        {
            if (string.IsNullOrEmpty(aasId))
                return new Result<ISubmodelDescriptor>(new ArgumentNullException(nameof(aasId)));
            if (string.IsNullOrEmpty(submodelId))
                return new Result<ISubmodelDescriptor>(new ArgumentNullException(nameof(submodelId)));
            if (submodelDescriptor == null)
                return new Result<ISubmodelDescriptor>(new ArgumentNullException(nameof(submodelDescriptor)));

            Uri uri = GetPath(AssetAdministrationShellRegistryRoutes.SHELL_DESCRIPTOR_ID_SUBMODEL_DESCRIPTOR_ID, aasId, submodelId);
            var request = base.CreateJsonContentRequest(uri, HttpMethod.Put, submodelDescriptor);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<ISubmodelDescriptor>(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult<IQueryableElementContainer<ISubmodelDescriptor>> RetrieveAllSubmodelRegistrations(string aasId, Predicate<ISubmodelDescriptor> predicate)
        {
            if (string.IsNullOrEmpty(aasId))
                return new Result<IQueryableElementContainer<ISubmodelDescriptor>>(new ArgumentNullException(nameof(aasId)));
            if (predicate == null)
                return new Result<IQueryableElementContainer<ISubmodelDescriptor>>(new ArgumentNullException(nameof(predicate)));

            Uri uri = GetPath(AssetAdministrationShellRegistryRoutes.SHELL_DESCRIPTOR_ID_SUBMODEL_DESCRIPTORS, aasId);
            var request = base.CreateRequest(uri, HttpMethod.Get);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<IEnumerable<ISubmodelDescriptor>>(response, response.Entity);

            if (!result.Success || result.Entity == null)
            {
                response?.Entity?.Dispose();
                return new Result<IQueryableElementContainer<ISubmodelDescriptor>>(result);
            }
            else
            {
                response?.Entity?.Dispose();
                var foundItems = result.Entity.Where(w => predicate.Invoke(w));
                return new Result<IQueryableElementContainer<ISubmodelDescriptor>>(result.Success, foundItems?.AsQueryableElementContainer(), result.Messages);
            }
        }

        public IResult<IQueryableElementContainer<ISubmodelDescriptor>> RetrieveAllSubmodelRegistrations(string aasId)
        {
            if (string.IsNullOrEmpty(aasId))
                return new Result<IQueryableElementContainer<ISubmodelDescriptor>>(new ArgumentNullException(nameof(aasId)));

            Uri uri = GetPath(AssetAdministrationShellRegistryRoutes.SHELL_DESCRIPTOR_ID_SUBMODEL_DESCRIPTORS, aasId);
            var request = base.CreateRequest(uri, HttpMethod.Get);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<IEnumerable<ISubmodelDescriptor>>(response, response.Entity);
            response?.Entity?.Dispose();
            return new Result<IQueryableElementContainer<ISubmodelDescriptor>>(result.Success, result.Entity?.AsQueryableElementContainer(), result.Messages);
        }

        public IResult<ISubmodelDescriptor> RetrieveSubmodelRegistration(string aasId, string submodelId)
        {
            if (string.IsNullOrEmpty(aasId))
                return new Result<ISubmodelDescriptor>(new ArgumentNullException(nameof(aasId)));
            if (string.IsNullOrEmpty(submodelId))
                return new Result<ISubmodelDescriptor>(new ArgumentNullException(nameof(submodelId)));

            Uri uri = GetPath(AssetAdministrationShellRegistryRoutes.SHELL_DESCRIPTOR_ID_SUBMODEL_DESCRIPTOR_ID, aasId, submodelId);
            var request = base.CreateRequest(uri, HttpMethod.Get);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<ISubmodelDescriptor>(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult DeleteSubmodelRegistration(string aasId, string submodelId)
        {
            if (string.IsNullOrEmpty(aasId))
                return new Result(new ArgumentNullException(nameof(aasId)));
            if (string.IsNullOrEmpty(submodelId))
                return new Result(new ArgumentNullException(nameof(submodelId)));

            Uri uri = GetPath(AssetAdministrationShellRegistryRoutes.SHELL_DESCRIPTOR_ID_SUBMODEL_DESCRIPTOR_ID, aasId, submodelId);
            var request = base.CreateRequest(uri, HttpMethod.Delete);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }   
    }
}
