global using FastEndpoints;
global using FastEndpoints.Security;
global using FastEndpoints.Swagger;
global using FluentValidation;
global using Riok.Mapperly.Abstractions;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Options;
global using Backend;
global using Backend.Extensions;
global using Backend.Features.Tenancy.Core.FeatureManagement;
global using Backend.Permissions;
global using Backend.ShareData;
global using Backend.Base;
global using Backend.Base.Dto;
global using Backend.ErrorHandling;
global using Backend.Attributes;
global using Backend.Exceptions;

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Backend.Tests")]