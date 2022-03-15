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
using BaSyx.API.Clients;
using BaSyx.Models.AdminShell;
using BaSyx.Utils.Client.Http;
using BaSyx.Utils.ResultHandling;
using System;
using System.Net.Http;
using BaSyx.Utils.FileSystem;
using BaSyx.Models.Connectivity;
using System.Linq;
using BaSyx.Utils.DependencyInjection;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using BaSyx.API.Http;

namespace BaSyx.Submodel.Client.Http
{
    public class SubmodelHttpClient : SimpleHttpClient, ISubmodelClient
    {
        private static readonly ILogger logger = LoggingExtentions.CreateLogger<SubmodelHttpClient>();

        public static bool USE_HTTPS = true;

        private const string SEPARATOR = "/";
        private const string SUBMODEL = "submodel";
        private const string SUBMODEL_ELEMENTS = "submodelElements";
        private const string VALUE = "value";
        private const string INVOKE = "invoke";
        private const string SYNCHRONOUS = "?async=false";
        private const string ASYNCHRONOUS = "?async=true";
        private const string INVOCATION_LIST = "invocationList";

        public Uri Endpoint { get; }

        private SubmodelHttpClient(HttpMessageHandler messageHandler) : base(messageHandler)
        {
            JsonSerializerSettings = new DependencyInjectionJsonSerializerSettings();
        }

        public SubmodelHttpClient(Uri endpoint) : this(endpoint, null)
        { }
        public SubmodelHttpClient(Uri endpoint, HttpMessageHandler messageHandler) : this(messageHandler)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));   
        }
        public SubmodelHttpClient(ISubmodelDescriptor submodelDescriptor) : this(submodelDescriptor, null)
        { }

        public SubmodelHttpClient(ISubmodelDescriptor submodelDescriptor, HttpMessageHandler messageHandler) : this(messageHandler)
        {
            submodelDescriptor = submodelDescriptor ?? throw new ArgumentNullException(nameof(submodelDescriptor));
            IEnumerable<HttpEndpoint> httpEndpoints = submodelDescriptor.Endpoints?.OfType<HttpEndpoint>();
            HttpEndpoint httpEndpoint = null;
            if (USE_HTTPS)
                httpEndpoint = httpEndpoints?.FirstOrDefault(p => p.Type == Uri.UriSchemeHttps);
            if (httpEndpoint == null)
                httpEndpoint = httpEndpoints?.FirstOrDefault(p => p.Type == Uri.UriSchemeHttp);

            if (httpEndpoint == null || string.IsNullOrEmpty(httpEndpoint.Address))
                throw new Exception("There is no http endpoint for instantiating a client");
            
            Endpoint = new Uri(httpEndpoint.Address.Replace(SubmodelRoutes.SUBMODEL, string.Empty));
        }

        public Uri GetUri(params string[] pathElements)
        {
            if (pathElements == null)
                return Endpoint;
            return Endpoint.Append(pathElements);
        }

        public IResult<ISubmodel> RetrieveSubmodel(RequestLevel level = default, RequestContent content = default, RequestExtent extent = default)
        {
            Uri uri = GetUri(SubmodelRoutes.SUBMODEL);
            var request = base.CreateRequest(uri, HttpMethod.Get);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<ISubmodel>(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult UpdateSubmodel(ISubmodel submodel)
        {
            throw new NotImplementedException();
        }

        public IResult<ISubmodelElement> CreateSubmodelElement(string idShortPath, ISubmodelElement submodelElement)
        {
            throw new NotImplementedException();
        }

        public IResult<ISubmodelElement> UpdateSubmodelElement(string rootSubmodelElementIdShort, ISubmodelElement submodelElement)
        {
            var request = base.CreateJsonContentRequest(GetUri(SUBMODEL_ELEMENTS, rootSubmodelElementIdShort), HttpMethod.Put, submodelElement);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<ISubmodelElement>(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult<IElementContainer<ISubmodelElement>> RetrieveSubmodelElements()
        {
            var request = base.CreateRequest(GetUri(SUBMODEL_ELEMENTS), HttpMethod.Get);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<IElementContainer<ISubmodelElement>>(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult<ISubmodelElement> RetrieveSubmodelElement(string submodelElementId)
        {
            var request = base.CreateRequest(GetUri(SUBMODEL_ELEMENTS, submodelElementId), HttpMethod.Get);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<ISubmodelElement>(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult<IValue> RetrieveSubmodelElementValue(string submodelElementId)
        {
            var request = base.CreateRequest(GetUri(SUBMODEL_ELEMENTS, submodelElementId, VALUE), HttpMethod.Get);
            var response = base.SendRequest(request, CancellationToken.None);
            IResult result = base.EvaluateResponse(response, response.Entity);
            response?.Entity?.Dispose();
            if (result.Success && result.Entity != null)
            {
                string sValue = Encoding.UTF8.GetString((byte[])result.Entity);
                if (!string.IsNullOrEmpty(sValue))
                {
                    object deserializedValue = JsonConvert.DeserializeObject(sValue);
                    return new Result<IValue>(result.Success, new ElementValue(deserializedValue, deserializedValue?.GetType()), result.Messages);
                }               
            }
            return new Result<IValue>(result);
        }      

        public IResult DeleteSubmodelElement(string submodelElementId)
        {
            var request = base.CreateRequest(GetUri(SUBMODEL_ELEMENTS, submodelElementId), HttpMethod.Delete);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult<InvocationResponse> InvokeOperation(string operationId, InvocationRequest invocationRequest)
        {
            var request = base.CreateJsonContentRequest(GetUri(SUBMODEL_ELEMENTS, operationId, INVOKE + SYNCHRONOUS), HttpMethod.Post, invocationRequest);

            TimeSpan timeout = request.GetTimeout() ?? GetDefaultTimeout();
            if (invocationRequest.Timeout.HasValue && invocationRequest.Timeout.Value > timeout.TotalMilliseconds)
                request.SetTimeout(TimeSpan.FromMilliseconds(invocationRequest.Timeout.Value));

            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<InvocationResponse>(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult<CallbackResponse> InvokeOperationAsync(string operationId, InvocationRequest invocationRequest)
        {
            var request = base.CreateJsonContentRequest(GetUri(SUBMODEL_ELEMENTS, operationId, INVOKE + ASYNCHRONOUS), HttpMethod.Post, invocationRequest);

            TimeSpan timeout = request.GetTimeout() ?? GetDefaultTimeout();
            if (invocationRequest.Timeout.HasValue && invocationRequest.Timeout.Value > timeout.TotalMilliseconds)
                request.SetTimeout(TimeSpan.FromMilliseconds(invocationRequest.Timeout.Value));

            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<CallbackResponse>(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult<InvocationResponse> GetInvocationResult(string operationId, string requestId)
        {
            var request = base.CreateRequest(GetUri(SUBMODEL_ELEMENTS, operationId, INVOCATION_LIST, requestId), HttpMethod.Get);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse<InvocationResponse>(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }

        public IResult UpdateSubmodelElementValue(string submodelElementId, IValue value)
        {
            var request = base.CreateJsonContentRequest(GetUri(SUBMODEL_ELEMENTS, submodelElementId, VALUE), HttpMethod.Put, value.Value);
            var response = base.SendRequest(request, CancellationToken.None);
            var result = base.EvaluateResponse(response, response.Entity);
            response?.Entity?.Dispose();
            return result;
        }
    }
}
