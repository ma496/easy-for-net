﻿using Autofac;
using CSharpTemplate.Common.Identity.Permissions.Provider;
using CSharpTemplate.Common.Tests.Base;
using FluentAssertions;
using Xunit.Abstractions;

namespace CSharpTemplate.Common.Tests.Identity.Permissions.Provider;

public class PermissionsContextTests : TestsBase
{
    public PermissionsContextTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public void PermissionsTest()
    {
        var context = Scope.Resolve<IPermissionsContext>();

        var crm = context.CreateGroup("CRM", "crm");
        
        var customersPermission = crm.AddPermission("Permissions.Customers", "Customers");
        customersPermission.AddChild("Permissions.Customers.Create", "Create");
        customersPermission.AddChild("Permissions.Customers.Update", "Update");
        customersPermission.AddChild("Permissions.Customers.Delete", "Delete");
        customersPermission.AddChild("Permissions.Developer.Customers.AutoGenerate", "AutoGenerate");
        crm.AddPermission("Permissions.Developer.AutoGenerate", "AutoGenerate");

        var setting = context.CreateGroup("Setting", "Setting");

        setting.AddPermission("Permissions.Setting", "Setting");
        setting.AddPermission("Permissions.Developer.Setting", "Setting");

        var permissionsByGroup = context.GetPermissionsByGroup();

        permissionsByGroup.Should().NotBeNullOrEmpty();
        permissionsByGroup.Should().HaveCount(2);
        permissionsByGroup.ToList()[0].Name.Should().Be("CRM");
        permissionsByGroup.ToList()[0].Permissions.Should().NotBeNullOrEmpty();
        permissionsByGroup.ToList()[0].Permissions.Should().HaveCount(1);
        permissionsByGroup.ToList()[0].Permissions.ToList()[0].Permissions.Should().HaveCount(3);
        permissionsByGroup.ToList()[0].Permissions.ToList()[0].Permissions.ToList()[0].Name.Should().Be("Permissions.Customers.Create");
        permissionsByGroup.ToList()[0].Permissions.ToList()[0].Permissions.ToList()[1].Name.Should().Be("Permissions.Customers.Update");
        permissionsByGroup.ToList()[0].Permissions.ToList()[0].Permissions.ToList()[2].Name.Should().Be("Permissions.Customers.Delete");
        
        permissionsByGroup.ToList()[1].Permissions.Should().HaveCount(1);
        permissionsByGroup.ToList()[1].Permissions.ToList()[0].Name.Should().Be("Permissions.Setting");

        var allPermissionsByGroup = context.GetAllPermissionsByGroup();

        allPermissionsByGroup.Should().NotBeNullOrEmpty();
        allPermissionsByGroup.ToList()[0].Permissions.Should().HaveCount(2);
        allPermissionsByGroup.ToList()[0].Permissions.ToList()[0].Permissions.Should().HaveCount(4);
        allPermissionsByGroup.ToList()[0].Permissions.ToList()[0].Permissions.ToList()[3].Name.Should()
            .Be("Permissions.Developer.Customers.AutoGenerate");
        allPermissionsByGroup.ToList()[0].Permissions.ToList()[1].Name.Should().Be("Permissions.Developer.AutoGenerate");
        
        allPermissionsByGroup.ToList()[1].Permissions.Should().HaveCount(2);
        allPermissionsByGroup.ToList()[1].Permissions.ToList()[1].Name.Should().Be("Permissions.Developer.Setting");
    }

    [Fact]
    public void FlatPermissionsTest()
    {
        var context = Scope.Resolve<IPermissionsContext>();

        var crm = context.CreateGroup("CRM", "crm");
        
        var customersPermission = crm.AddPermission("Permissions.Customers", "Customers");
        customersPermission.AddChild("Permissions.Customers.Create", "Create");
        customersPermission.AddChild("Permissions.Customers.Update", "Update");
        customersPermission.AddChild("Permissions.Customers.Delete", "Delete");
        customersPermission.AddChild("Permissions.Developer.Customers.AutoGenerate", "AutoGenerate");
        crm.AddPermission("Permissions.Developer.AutoGenerate", "AutoGenerate");

        var setting = context.CreateGroup("Setting", "Setting");

        setting.AddPermission("Permissions.Setting", "Setting");
        setting.AddPermission("Permissions.Developer.Setting", "Setting");

        var permissions = context.GetFlatPermissions();

        permissions.Should().HaveCount(5);
        permissions.ToList()[0].Should().Be("Permissions.Customers");
        permissions.ToList()[1].Should().Be("Permissions.Customers.Create");
        permissions.ToList()[2].Should().Be("Permissions.Customers.Update");
        permissions.ToList()[3].Should().Be("Permissions.Customers.Delete");
        permissions.ToList()[4].Should().Be("Permissions.Setting");

        var allPermissions = context.GetFlatAllPermissions();

        allPermissions.Should().HaveCount(8);
        allPermissions.ToList()[0].Should().Be("Permissions.Customers");
        allPermissions.ToList()[1].Should().Be("Permissions.Customers.Create");
        allPermissions.ToList()[2].Should().Be("Permissions.Customers.Update");
        allPermissions.ToList()[3].Should().Be("Permissions.Customers.Delete");
        allPermissions.ToList()[4].Should().Be("Permissions.Developer.Customers.AutoGenerate");
        allPermissions.ToList()[5].Should().Be("Permissions.Developer.AutoGenerate");
        allPermissions.ToList()[6].Should().Be("Permissions.Setting");
        allPermissions.ToList()[7].Should().Be("Permissions.Developer.Setting");
    }
}