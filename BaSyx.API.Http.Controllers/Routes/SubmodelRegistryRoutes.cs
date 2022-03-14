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
    /// The collection of a all Submodel Registry routes
    /// </summary>
    public static class SubmodelRegistryRoutes
    {
        /// <summary>
        /// Root route
        /// </summary>
        public const string SUBMODEL_DESCRIPTORS = "/registry/submodel-descriptors";
        /// <summary>
        /// Specific Submodel Descriptor
        /// </summary>
        public const string SUBMODEL_DESCRIPTOR_ID = "/registry/submodel-descriptors/{submodelIdentifier}";
    }
}
