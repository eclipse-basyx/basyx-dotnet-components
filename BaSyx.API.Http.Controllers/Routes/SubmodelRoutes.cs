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
    /// The collection of a all Submodel routes
    /// </summary>
    public static class SubmodelRoutes
    {
        /// <summary>
        /// Root route
        /// </summary>
        public const string SUBMODEL = "/submodel";
        /// <summary>
        /// Submodel table format route
        /// </summary>
        public const string SUBMODEL_TABLE = "/submodel/table";
        /// <summary>
        /// Submodel elements route
        /// </summary>
        public const string SUBMODEL_ELEMENTS = "/submodel/submodel-elements";
        /// <summary>
        /// Submodel elements idShortPath route
        /// </summary>
        public const string SUBMODEL_ELEMENTS_IDSHORTPATH = "/submodel/submodel-elements/{idShortPath}";
        /// <summary>
        /// Submodel operation idShortPath route
        /// </summary>
        public const string SUBMODEL_ELEMENTS_IDSHORTPATH_INVOKE = "/submodel/submodel-elements/{idShortPath}/invoke";
        /// <summary>
        /// Submodel file element upload route
        /// </summary>
        public const string SUBMODEL_ELEMENTS_IDSHORTPATH_UPLOAD = "/submodel/submodel-elements/{idShortPath}/upload";
        /// <summary>
        /// Submodel asyncronous operation result route
        /// </summary>
        public const string SUBMODEL_ELEMENTS_IDSHORTPATH_OPERATION_RESULTS = "/submodel/submodel-elements/{idShortPath}/operation-results/{handleId}";
    }
}
