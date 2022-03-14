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

namespace BaSyx.API.Http.Controllers
{
    /// <summary>
    /// The collection of a all Asset Adminstration Shell Registry routes
    /// </summary>
    public static class AssetAdministrationShellRegistryRoutes
    {
        /// <summary>
        /// Root route
        /// </summary>
        public const string SHELL_DESCRIPTORS = "/registry/shell-descriptors";
        /// <summary>
        /// Specific Asset Administration Shell Descriptor
        /// </summary>
        public const string SHELL_DESCRIPTOR_ID = "/registry/shell-descriptors/{aasIdentifier}";
        /// <summary>
        /// Submodel Descriptors
        /// </summary>
        public const string SHELL_DESCRIPTOR_ID_SUBMODEL_DESCRIPTORS = "/registry/shell-descriptors/{aasIdentifier}/submodel-descriptors";
        /// <summary>
        /// Specific Submodel Descriptor
        /// </summary>
        public const string SHELL_DESCRIPTOR_ID_SUBMODEL_DESCRIPTOR_ID = "/registry/shell-descriptors/{aasIdentifier}/submodel-descriptors/{submodelIdentifier}";
    }
}
