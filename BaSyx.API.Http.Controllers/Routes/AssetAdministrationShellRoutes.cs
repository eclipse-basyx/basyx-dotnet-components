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
    /// The collection of a all Asset Adminstration Shell routes
    /// </summary>
    public static class AssetAdministrationShellRoutes
    {
        /// <summary>
        /// Root route
        /// </summary>
        public const string AAS = "/aas";
        /// <summary>
        /// Asset Information
        /// </summary>
        public const string AAS_ASSET_INFORMATION = "/aas/asset-information";
        /// <summary>
        /// Asset Information
        /// </summary>
        public const string AAS_SUBMODELS = "/aas/submodels";
        /// <summary>
        /// Asset Information
        /// </summary>
        public const string AAS_SUBMODELS_BYID = "/aas/submodels/{submodelIdentifier}";

    }
}
