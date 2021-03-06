﻿# Sql Server REST API - OData Service

Sql Server REST API library enables you to easily create OData services. In this page you can find out how to create
OData service and what features from OData standard are supported.

# Supported query parameters

Sql Server REST API library enables you to create OData REST services that support the following operations:
 - $select that enables the caller to choose what fields should be returned in response,
 - $filter that can filter entities using entity properties, and expressions with:
   - Arithmetical operators 'add', 'sub', 'mul', 'div' , 'mod'
   - Relational operators 'eq', 'ne', 'gt', 'ge', 'lt', 'le'
   - Logical operators 'and', 'or', 'not', 	
   - Build-in functions: 'length', 'tolower', 'toupper', 'year', 'month', 'day', 'hour', 'minute', and 'second',
   - Non-standard functions: 'json_value', 'json_query', and 'isjson'
 - $orderby that can sort entities by some column(s)
 - $top and $skip that can be used for pagination,
 - $count that enables you to get the total number of entities,
 - $search that search for entities by a keyword,
 - $expand that fetches related entries.

OData services implemented using Sql Server REST API library provide minimal interface that web clients can use to
query data without additional overhead introduced by advanced OData operators, or verbose response format.

> The goal of this project is not to support all standard OData features. Library provides the most important features, and
> excludes features that cannot provide optimal performance. The most important benefits that this library provides are simplicity and speed. 
> If you need full compatibility with official OData spec, you can chose other implementations.

# Metadata information

OData services implemented using Sql Server REST API library return minimal response format that is compliant to the
OData spec. By default, it returns no-metadata; however, it can be configured to output minimal metadata.

# Implement OData service

OData service can be implemented using any .Net project, such ASP.NET Core, ASP.NET Web Api, Azure Function (C#). You just need to reference nuget package in project.json file:

''project.json''
```
{
  "frameworks": {
    "net46":{
      "dependencies": {
        "Antlr4.Runtime": "4.6.5",
        "MsServer.RestApi": "0.4"
      }
    }
   }
}
```
This setting will take Sql Server Rest Api from NuGet and also load Antlr4.Runtime that is used to parse requests. Once you reference these nuget packages, you can create your OData service.

## No-metadata OData service and ASP.NET Core

You can implement OData service using ASP.NET Core application as a method of any controller. 
First, you need to setup Sql Client in Startup class: 
 - Add the reference to ''SqlServerRestApi'' in Startup class
 - Initialize SqlClient component that will be used to read data from table and return it as OData response.

Example of code is shown in the following listing:

''Startup.cs''
```
using SqlServerRestApi;

namespace MyMvcApp
{
    public class Startup {

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSqlClient(Configuration["ConnectionStrings:azure-db-connection"]);
            // Add framework services.
            services.AddMvc();
        }

    }
}
```

Then you need to create a controller that will expose OData service using some method.
 - Add references to Belgrade.SqlClient and SqlServerApi namespace in controller,
 - Initialize IQueryPipe field using constructor injection,
 - Create a method that will expose OData REST Api.

```
using Belgrade.SqlClient;
using SqlServerRestApi;

namespace MyApp.Controllers
{
    [Route("api/[controller]")]
    public class PeopleController : Controller
    {
        IComand cmd = null;
        public PeopleController(ICommand sqlQueryService)
        {
            this.cmd = sqlQueryService;
        }

        /// <summary>
        /// Endpoint that exposes People information using OData protocol.
        /// </summary>
        /// <returns>OData response.</returns>
        // GET api/People/odata
        [HttpGet("odata")]
        public async Task OData()
        {
            var tableSpec = new TableSpec(schema: "Application", table: "People", columns: "PersonID,FullName,PhoneNumber");
            await this.OData(tableSpec, cmd).Process();
        }

		[HttpGet("odata")]
        public async Task People()
        {
            var tableSpec = new TableSpec(schema: "Application", table: "People", columns: "PersonID,FullName,PhoneNumber,FaxNumber,EmailAddress,ValidTo")
                .AddRelatedTable("Orders", "Sales", table: "Orders", columns: "Application.People.PersonID = Sales.Orders.CustomerID", "OrderID,OrderDate,ExpectedDeliveryDate,Comments")
                .AddRelatedTable("Invoices", "Sales", "Invoices", "Application.People.PersonID = Sales.Invoices.CustomerID", "InvoiceID,InvoiceDate,IsCreditNote,Comments");
            await this.OData(tableSpec, queryService).Process();
        }
    }
}
```

OData service enables yo to fetch the information about related entities, such as Sales.Orders or Sales.Invoices that pelong to a person.
You wouldneed to describe relationships in the table specification before you process the OData request.
```
	[HttpGet("odata")]
	public async Task OData()
	{
		var tableSpec = new TableSpec(schema: "Application", name: "People", columnList: "PersonID,FullName,PhoneNumber,FaxNumber,EmailAddress,ValidTo")
			.AddRelatedTable(relation: "Orders", schema: "Sales", name: "Orders", columnList: "Application.People.PersonID = Sales.Orders.CustomerID", "OrderID,OrderDate,ExpectedDeliveryDate,Comments")
			.AddRelatedTable(relation: "Invoices", schema: "Sales", name: "Invoices", columnList: "Application.People.PersonID = Sales.Invoices.CustomerID", "InvoiceID,InvoiceDate,IsCreditNote,Comments");
		await this.OData(tableSpec, this.cmd).Process();
	}
```


When you run this app and open http://......./api/People/odata Url, you would be able to call all supported functions in OData service.

## No-metadata OData service and Azure Function

Azure Functions are lightweight components that you can use to create some function in C#, Node.JS, or other languages, and expose the function
as API. This might be combined with OData since you can create single Azure Function that will handle OData requests.

```
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using MsSql.RestApi;
using System;
using System.Threading.Tasks;

namespace TestFunction
{
    public static class ODataResult
    {
        [FunctionName("ODataResult")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            try
            {
                var tableSpec = new TableSpec(schema: "sys", table: "objects", columns: "object_id,name,type,schema_id,create_date");
                return await req.OData(tableSpec).GetResult(Environment.GetEnvironmentVariable("SqlDb"));
            }
            catch (Exception ex)
            {
                log.LogError($"C# Http trigger function exception: {ex.Message}");
                return new StatusCodeResult(500);
            }
        }
    }
}
```

You need to setup connection string to your database in some key (e.g. "azure-db-connection"), get that connection string, create SqlPipe that
will stream results into Response output stream. You will need to create ''TableSpec'' object that describes source table or view with the name
object and list of columns that will be exposed. In this example, OData exposes five columns from sys.object view.

When you call this Url, you can add any OData parameter to filter or sort results.

## Minimal metadata OData service with ASP.NET Core

Some clients such as LinqPad require at least minimal OData metadata infor to use OData service. In this case you can create a OData service with minimal-metadata.

Then you need to create a controller that will expose OData service using some method.
 - Add references to Belgrade.SqlClient and SqlServerApi namespace in controller,
 - Derive Controller from OData Controller,
 - Initialize IQueryPipe field using constructor injection,
 - Define properties of the tables that will be exposed via OData endpoints by overriding GetTableSpec method,
 - Create methods that will expose OData REST Api.

```
using Belgrade.SqlClient;
using SqlServerRestApi;

namespace MyMvcApp.Controllers
{
    [Route("api/[controller]")]
    public class PeopleController : ODataController
    {
        IQueryPipe pipe = null;
        public PeopleController(IQueryPipe sqlQueryService)
        {
            this.pipe = sqlQueryService;
        }

        [HttpGet("table")]
        public async Task Table()
        {
            var tableSpec = new TableSpec(schema: "Application", table: "People", columns: "FullName,EmailAddress,PhoneNumber,FaxNumber");
            await this
                    .Table(tableSpec)
                    .Process(this.pipe);
        }
    }
}
```

You can generate table specification directly using T-SQL query by querying system views:

```
select CONCAT('new TableSpec("',schema_name(t.schema_id), '","', t.name, '")') +
	string_agg(CONCAT('.AddColumn("', c.name, '", "', tp.name, '", isKeyColumn:', IIF(ix.is_primary_key = 1, 'true', 'false'), '))'),'')
from sys.tables t
	join sys.columns c on t.object_id = c.object_id
	join sys.types tp on c.system_type_id = tp.system_type_id
	left join sys.index_columns ic on c.column_id = ic.column_id and c.object_id = ic.object_id
	left join sys.indexes ix on ic.index_id = ix.index_id and ic.object_id = ix.object_id
--where t.name in ('','','') --> specify target tables if needed.
group by t.schema_id, t.name
```

Text generated with this query can be copied into controller body.
