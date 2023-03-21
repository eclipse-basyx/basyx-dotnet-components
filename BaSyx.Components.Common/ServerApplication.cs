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
using BaSyx.Components.Common.Abstractions;
using BaSyx.Utils.AssemblyHandling;
using BaSyx.Utils.DependencyInjection;
using BaSyx.Utils.JsonHandling;
using BaSyx.Utils.ResultHandling;
using BaSyx.Utils.Settings.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace BaSyx.Components.Common
{
    public abstract class ServerApplication : IServerApplication
    {
        private static readonly ILogger logger = LoggingExtentions.CreateLogger<ServerApplication>();

        private string _contentRoot;
        private string _webRoot;
        private bool _secure = false;
        private string _defaultRoute = null;
        private string _pathBase = null;

        private readonly List<Action<IApplicationBuilder>> AppBuilderPipeline;
        private readonly List<Action<IServiceCollection>> ServiceBuilderPipeline;
        private readonly List<Action<IEndpointRouteBuilder>> EndpointBuilderPipeline;

        public const string DEFAULT_CONTENT_ROOT = "Content";
        public const string DEFAULT_WEB_ROOT = "wwwroot";
        public const string UI_RELATIVE_PATH = "/ui";
        public const string FILES_PATH = "/files";
        public const string ERROR_PATH = "/error";
        public const string CONTROLLER_ASSEMBLY_NAME = "BaSyx.API.Http.Controllers";

        public Assembly ControllerAssembly { get; private set; }
        public ServerSettings Settings { get; protected set; }
        public IWebHostBuilder WebHostBuilder { get; protected set; }
               
        public string ExecutionPath { get; }

        public Action ApplicationStarted { get; set; }

        public Action ApplicationStopping { get; set; }

        public Action ApplicationStopped { get; set; }

        protected ServerApplication() : this(null, null)
        { }
        protected ServerApplication(ServerSettings settings) : this(settings, null)
        { }
        protected ServerApplication(ServerSettings settings, string[] webHostBuilderArgs)
        {
            ExecutionPath = settings?.ExecutionPath ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ControllerAssembly = Assembly.Load(CONTROLLER_ASSEMBLY_NAME);

            if (settings == null && !EmbeddedResource.CheckOrWriteRessourceToFile(typeof(ServerApplication).Assembly, Path.Combine(ExecutionPath, "ServerSettings.xml")))
                logger.LogError("ServerSettings.xml cannot be loaded or written");

            Settings = settings ?? ServerSettings.LoadSettingsFromFile("ServerSettings.xml") ?? throw new ArgumentNullException(nameof(settings));

            webHostBuilderArgs ??= Environment.GetCommandLineArgs();
            WebHostBuilder = DefaultWebHostBuilder.CreateWebHostBuilder(webHostBuilderArgs, Settings);
            AppBuilderPipeline = new List<Action<IApplicationBuilder>>();
            ServiceBuilderPipeline = new List<Action<IServiceCollection>>();
            EndpointBuilderPipeline = new List<Action<IEndpointRouteBuilder>>();

            WebHostBuilder.ConfigureServices( (context, services) =>
            {
                ConfigureServices(context, services);
            });

            WebHostBuilder.Configure(app =>
            {
                Configure(app);
            });

            ConfigureLogging(LogLevel.Trace);

            if (string.IsNullOrEmpty(Settings.ServerConfig.Hosting.ContentPath))
                _contentRoot = Path.Join(ExecutionPath, DEFAULT_CONTENT_ROOT);
            else if (Path.IsPathRooted(Settings.ServerConfig.Hosting.ContentPath))
                _contentRoot = Settings.ServerConfig.Hosting.ContentPath;
            else
                _contentRoot = Path.Join(ExecutionPath, Settings.ServerConfig.Hosting.ContentPath);

            try
            {
                if (!Directory.Exists(_contentRoot))
                    Directory.CreateDirectory(_contentRoot);
                WebHostBuilder.UseContentRoot(_contentRoot);
            }
            catch (Exception e)
            {
                logger.LogError(e, $"ContentRoot path {_contentRoot} cannot be created ");
            }

            _webRoot = Path.Join(ExecutionPath, DEFAULT_WEB_ROOT);
            _defaultRoute = string.IsNullOrEmpty(Settings.ServerConfig.DefaultRoute) ? UI_RELATIVE_PATH : Settings.ServerConfig.DefaultRoute;

            try
            {
                if (!Directory.Exists(_webRoot))
                    Directory.CreateDirectory(_webRoot);
                WebHostBuilder.UseWebRoot(_webRoot);
                logger.LogInformation($"wwwroot-Path: {_webRoot}");
            }
            catch (Exception e)
            {
                logger.LogError(e, $"WebRoot path {_webRoot} cannot be created ");
            }            
        }

        public virtual void Run()
        {
            logger.LogInformation("Starting Server...");

            WebHostBuilder.Build().Run();
        }
        public virtual async Task RunAsync(CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Starting Server asynchronously...");

            await WebHostBuilder.Build().RunAsync(cancellationToken);
        }
        public virtual void ConfigureLogging(LogLevel minimumLogLevel)
        {
            WebHostBuilder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(minimumLogLevel);
            });
        }
        public virtual void Configure(Action<IApplicationBuilder> app) => AppBuilderPipeline.Add(app);
        public virtual void ConfigureServices(Action<IServiceCollection> services) => ServiceBuilderPipeline.Add(services);
        public virtual void ConfigureEndpoints(Action<IEndpointRouteBuilder> endpoints) => EndpointBuilderPipeline.Add(endpoints);
        public virtual void UseContentRoot(string contentRoot)
        {
            _contentRoot = contentRoot;
            WebHostBuilder.UseContentRoot(contentRoot);
        }
        public virtual void UseWebRoot(string webRoot)
        {
            _webRoot = webRoot;
            WebHostBuilder.UseWebRoot(webRoot);
        }
        public virtual void UseUrls(params string[] urls)
        {
            WebHostBuilder.UseUrls(urls);
            if (Settings?.ServerConfig?.Hosting != null)
                Settings.ServerConfig.Hosting.Urls = urls?.ToList();
        }

        public virtual void UsePathBase(string pathBase)
        {
            _pathBase= pathBase;
        }

        public virtual void ProvideContent(Uri relativeUri, Stream content)
        {
            try
            {
                using (Stream stream = content)
                {
                    string fileName = Path.GetFileName(relativeUri.ToString());
                    logger.LogInformation("FileName: " + fileName);
                    string directory = Path.GetDirectoryName(relativeUri.ToString()).TrimStart('\\');
                    logger.LogInformation("Directory: " + directory);

                    string hostingDirectory = Path.Join(_contentRoot, directory);

                    logger.LogInformation($"Try creating hosting directory if not existing: {hostingDirectory}");
                    Directory.CreateDirectory(hostingDirectory);

                    string filePath = Path.Join(hostingDirectory, fileName);
                    logger.LogInformation($"Try writing file: {filePath}");

                    using (FileStream fileStream = File.OpenWrite(filePath))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Error providing content {relativeUri}");
            }
        }
        public virtual void MapControllers(ControllerConfiguration controllerConfig)
        {
            this.ConfigureServices(services =>
            {
                if (controllerConfig?.Controllers?.Count > 0)
                {
                    var mvcBuilder = services.AddMvc();
                    foreach (var controllerAssemblyString in controllerConfig.Controllers)
                    {
                        Assembly controllerAssembly = null;
                        try
                        {
                            controllerAssembly = Assembly.Load(controllerAssemblyString);
                        }
                        catch (Exception e)
                        {
                            logger.LogWarning(e, $"Assembly {controllerAssemblyString} cannot be loaded - maybe it is not referenced. Try reading from file...");
                            try
                            {
                                if (File.Exists(controllerAssemblyString))
                                    controllerAssembly = Assembly.LoadFile(controllerAssemblyString);
                                else if (File.Exists(controllerAssemblyString + ".dll"))
                                    controllerAssembly = Assembly.LoadFile(controllerAssemblyString + ".dll");
                                else
                                    controllerAssembly = Assembly.LoadFrom(controllerAssemblyString);
                            }
                            catch (Exception exp)
                            {
                                logger.LogWarning(exp, $"Assembly {controllerAssemblyString} can finally not be loaded");
                            }
                        }
                        if (controllerAssembly != null)
                        {
                            mvcBuilder.AddApplicationPart(controllerAssembly);
                            string controllerAssemblyName = controllerAssembly.GetName().Name;
                            string xmlDocFile = $"{controllerAssemblyName}.xml";
                            string xmlDocFilePath = Path.Combine(ExecutionPath, xmlDocFile);

                            if (File.Exists(xmlDocFilePath))
                                continue;

                            try
                            {
                                ManifestEmbeddedFileProvider embeddedFileProvider = new ManifestEmbeddedFileProvider(controllerAssembly);
                                IFileInfo fileInfo = embeddedFileProvider.GetFileInfo(xmlDocFile);
                                if (fileInfo == null)
                                {
                                    logger.LogWarning($"{xmlDocFile} of Assembly {controllerAssemblyName} not found");
                                    continue;
                                }
                                using (Stream stream = fileInfo.CreateReadStream())
                                {
                                    using (FileStream fileStream = File.OpenWrite(xmlDocFilePath))
                                    {
                                        stream.CopyTo(fileStream);
                                    }
                                }
                                logger.LogInformation($"{xmlDocFile} of Assembly {controllerAssemblyName} has been created successfully");
                            }
                            catch (Exception e)
                            {
                                logger.LogWarning(e, $"{xmlDocFile} of Assembly {controllerAssemblyName} cannot be read");
                            }
                        }
                    }
                    mvcBuilder.AddControllersAsServices();
                }
            });
        }
        protected virtual void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
        {
            services.AddSingleton(typeof(ServerSettings), Settings);
            services.AddSingleton<IServerApplicationLifetime>(this);

            
            var urls = Settings.ServerConfig.Hosting.Urls;
            var secureUrl = urls.Find(s => s.StartsWith("https"));
            if (!string.IsNullOrEmpty(secureUrl) && !context.HostingEnvironment.IsDevelopment())
            {
                secureUrl = secureUrl.Replace("+", "0.0.0.0");
                Uri secureUri = new Uri(secureUrl);
                _secure = true;
                services.AddHttpsRedirection(opts =>
                {
                    opts.HttpsPort = secureUri.Port;   
                });
            }

            services.AddStandardImplementation();

            services.AddCors();
            services
                .AddMvc(options =>
                {
                    options.InputFormatters.RemoveType<Microsoft.AspNetCore.Mvc.Formatters.SystemTextJsonInputFormatter>();
                    options.OutputFormatters.RemoveType<Microsoft.AspNetCore.Mvc.Formatters.SystemTextJsonOutputFormatter>();
                    options.RespectBrowserAcceptHeader = true;
                })
                .AddApplicationPart(ControllerAssembly)
                .AddControllersAsServices()
                .AddNewtonsoftJson(options =>
                {
                    options.GetDefaultMvcJsonOptions(services);
                });

            services.AddRazorPages(opts =>
            {
                logger.LogInformation("Pages-RootDirectory: " + opts.RootDirectory);
            });

            services.AddDirectoryBrowser();

            services.PostConfigure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = actionContext =>
                {
                    ValidationProblemDetails details = actionContext.ModelState
                        .Where(e => e.Value.Errors.Count > 0)
                        .Select(e => new ValidationProblemDetails(actionContext.ModelState)).FirstOrDefault();

                    List<IMessage> messages = new List<IMessage>();
                    messages.Add(new Message(
                           MessageType.Error,
                           $"Path '{actionContext.HttpContext.Request.Path.Value}' received invalid or malformed request",
                           actionContext.HttpContext.Response.StatusCode.ToString()));

                    foreach (var error in details.Errors.Values)
                    {
                        string joinedError = string.Join(" | ", error);
                        messages.Add(new Message(MessageType.Error, joinedError));
                    }

                    Result result = new Result(false, messages);

                    return new BadRequestObjectResult(result);
                };
            });

            foreach (var serviceBuider in ServiceBuilderPipeline)
            {
                serviceBuider.Invoke(services);
            }
        }
        protected virtual void Configure(IApplicationBuilder app)
        {
            var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
            var applicationLifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
            var loggingFactory = app.ApplicationServices.GetRequiredService<ILoggerFactory>();
            
            //Set ASP.NET Core LoggingFactory to global LoggingFactory for all other loggings within the BaSyx SDK
            LoggingExtentions.LoggerFactory = loggingFactory;

            if (env.IsProduction())
                app.UseHsts();

            if (!string.IsNullOrEmpty(_pathBase))
            {
                app.UsePathBase(_pathBase);
                app.Use(async (context, next) =>
                {
                    if (!context.Request.PathBase.HasValue)
                    {
                        context.Response.Redirect(_pathBase + context.Request.Path);
                        return;
                    }
                    await next();
                });
            }

            app.UseExceptionHandler(ERROR_PATH);

            app.UseStatusCodePages(async context =>
            {
                context.HttpContext.Response.ContentType = "application/json";

                JsonSerializerSettings settings;
                var options = context.HttpContext.RequestServices.GetService<MvcNewtonsoftJsonOptions>();
                if (options == null)
                    settings = new DefaultJsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
                else
                    settings = options.SerializerSettings;

                Result result = new Result(false, 
                    new Message(MessageType.Error, "Path: " + context.HttpContext.Request.Path.Value, 
                    context.HttpContext.Response.StatusCode.ToString()));
                              
                string resultMessage = JsonConvert.SerializeObject(result, settings);

                await context.HttpContext.Response.WriteAsync(resultMessage);
            });

            if(_secure && !env.IsDevelopment())
                app.UseHttpsRedirection();

            foreach (var appBuilder in AppBuilderPipeline)
            {
                appBuilder.Invoke(app);
            }

            app.UseStaticFiles();

            string path = env.ContentRootPath;
            if (Directory.Exists(path))
            {
                app.UseStaticFiles(new StaticFileOptions()
                {
                    FileProvider = new PhysicalFileProvider(@path),
                    RequestPath = new PathString(FILES_PATH),
                    ServeUnknownFileTypes = true
                });

                app.UseDirectoryBrowser(new DirectoryBrowserOptions
                {
                    FileProvider = new PhysicalFileProvider(@path),
                    RequestPath = new PathString(FILES_PATH)
                });
            }

            app.Use((context, next) =>
            {
                string requestPath = context.Request.Path.ToUriComponent();
                
                if (requestPath.Contains("submodelElements/"))
                {
                    Match valueMatch = Regex.Match(requestPath, "(?<=submodelElements/)(.*)(?=/value|/invoke|/invocationList|/upload)");
                    if(valueMatch.Success && !string.IsNullOrEmpty(valueMatch.Value))
                    {
                        string elementPath = HttpUtility.UrlEncode(valueMatch.Value);
                        requestPath = requestPath.Replace(valueMatch.Value, elementPath);
                        context.Request.Path = new PathString(requestPath);
                    }
                    else
                    {
                        Match baseMatch = Regex.Match(requestPath, "(?<=submodelElements/)(.*)");
                        if(baseMatch.Success && !string.IsNullOrEmpty(baseMatch.Value))
                        {
                            string elementPath = HttpUtility.UrlEncode(baseMatch.Value);
                            requestPath = requestPath.Replace(baseMatch.Value, elementPath);
                            context.Request.Path = new PathString(requestPath);
                        }
                    }
                }
                return next();
            });            

            app.UseRouting();

            app.UseCors(
                options => options
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowAnyOrigin()
            );
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();

                foreach (var endpointBuilder in EndpointBuilderPipeline)
                {
                    endpointBuilder.Invoke(endpoints);
                }
            });

            var options = new RewriteOptions().AddRedirect("^$", _defaultRoute);
            app.UseRewriter(options);

            if (ApplicationStarted != null)
                applicationLifetime.ApplicationStarted.Register(ApplicationStarted);
            if (ApplicationStopping != null)
                applicationLifetime.ApplicationStopping.Register(ApplicationStopping);
            if (ApplicationStopped != null)
                applicationLifetime.ApplicationStopped.Register(ApplicationStopped);            
        }
    }
}
