using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VepayDO.Api.Tests.Probe;
using VepayDO.Application.Auth;
using VepayDO.Application.BusinessAccounts;
using VepayDO.Application.Charges;
using VepayDO.Application.Configuration;
using VepayDO.Application.Customers;
using VepayDO.Application.Legal;
using VepayDO.Application.Members;
using VepayDO.Application.Receipts;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Application.Tenants;

namespace VepayDO.Api.Tests.Fixtures;

public partial class ApiTestFactory
{

    private const string DatabaseUrl = "postgresql://vepaydo:vepaydo@127.0.0.1:59999/vepaydo_test";
}
