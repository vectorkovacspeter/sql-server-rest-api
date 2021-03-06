﻿// Copyright (c) Jovan Popovic. All Rights Reserved.
// Licensed under the BSD License. See LICENSE.txt in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using MsSql.RestApi;

namespace MsSql.OData
{
    /// <summary>
    /// Controller class that should be used to expose OData REST API with minimal metadata.
    /// </summary>
    public abstract class ODataController : Controller
    {
        /// <summary>
        /// Url that will be placed in XML metadata.
        /// </summary>
        public virtual string MetadataUrl
        {
            get
            {
                return this.Request.Scheme + "://" + this.Request.Host + "/" + this.ControllerContext.ActionDescriptor.AttributeRouteInfo.Template + "/$metadata";
            }
        }

        public virtual string ModelNamespace
        {
            get
            {
                return this.ControllerContext.ActionDescriptor.ControllerName + ".Models";
            }
        }

        public abstract TableSpec[] GetTableSpec { get; }

        [Produces("application/json; odata.metadata=minimal")]
        [HttpGet]
        public string Root()
        {
            return ODataHandler.GetRootMetadataJsonV4(this.MetadataUrl, this.GetTableSpec);
        }

        [Produces("application/xml")]
        [HttpGet("$metadata")]
        public string Metadata()
        {
            return ODataHandler.GetMetadataXmlV4(this.GetTableSpec, this.ModelNamespace);
        }
    }
}
