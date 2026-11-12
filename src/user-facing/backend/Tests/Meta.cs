global using FluentAssertions;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.EntityFrameworkCore;
global using System.Net;
global using Xunit;
global using Bogus;
global using FastEndpoints;
global using FastEndpoints.Testing;
global using FastEndpoints.Security;
global using FluentValidation;
global using Riok.Mapperly.Abstractions;
global using Backend;
global using Backend.Extensions;
global using Backend.Permissions;
global using Backend.ShareData;
global using Backend.Base;
global using Backend.Base.Dto;
global using Backend.ErrorHandling;
global using Backend.Tests.Seeder;

// The application under test is built once for the whole assembly. Do not replace this with
// FastEndpoints' [assembly: EnableAdvancedTesting] and TestBaseWithAssemblyFixture<App>: that test
// framework disables parallelization for the entire assembly, which is the one thing this suite
// cannot give up.
[assembly: AssemblyFixture(typeof(Backend.Tests.App))]
