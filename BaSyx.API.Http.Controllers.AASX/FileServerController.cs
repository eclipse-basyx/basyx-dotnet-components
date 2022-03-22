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
using Microsoft.AspNetCore.Mvc;
using BaSyx.API.ServiceProvider;
using BaSyx.Models.Export;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using System.IO;
using BaSyx.Models.AdminShell;
using System.Linq;
using BaSyx.Utils.FileSystem;

namespace BaSyx.API.Http.Controllers.PackageService
{
    /// <summary>
    /// The File Server Controller Controller
    /// </summary>
    public class FileServerController : Controller
    {
        private readonly IServiceProvider serviceProvider;

#if NETCOREAPP3_1
        private readonly IWebHostEnvironment hostingEnvironment;

        /// <summary>
        /// Constructor for the File Server Controller
        /// </summary>
        /// <param name="serviceProvider">The Service Provider implementation provided by the dependency injection</param>
        /// <param name="environment">The Hosting Environment provided by the dependency injection</param>
        public PackageService(IServiceProvider serviceProvider, IWebHostEnvironment environment)
        {
            this.serviceProvider = serviceProvider;
            hostingEnvironment = environment;
        }
#else
        private readonly IHostingEnvironment hostingEnvironment;

        /// <summary>
        /// Constructor for the File Server Controller
        /// </summary>
        /// <param name="serviceProvider">The Service Provider implementation provided by the dependency injection</param>
        /// <param name="environment">The Hosting Environment provided by the dependency injection</param>
        public FileServerController(IServiceProvider serviceProvider, IHostingEnvironment environment)
        {
            this.serviceProvider = serviceProvider;
            hostingEnvironment = environment;
        }


#endif

     
    }
}