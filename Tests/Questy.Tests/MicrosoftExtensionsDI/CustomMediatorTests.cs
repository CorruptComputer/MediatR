﻿using Microsoft.Extensions.DependencyInjection;

namespace Questy.Extensions.Microsoft.DependencyInjection.Tests;

using System;
using System.Linq;
using Shouldly;
using Xunit;

public class CustomMediatorTests
{
    private readonly IServiceProvider _provider;

    public CustomMediatorTests()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(new Logger());
        services.AddMediator(cfg =>
        {
            cfg.MediatorImplementationType = typeof(MyCustomMediator);
            cfg.RegisterServicesFromAssemblyContaining(typeof(CustomMediatorTests));
        });
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public void ShouldResolveMediator()
    {
        _provider.GetService<IMediator>().ShouldNotBeNull();
        _provider.GetRequiredService<IMediator>().GetType().ShouldBe(typeof(MyCustomMediator));
    }

    [Fact]
    public void ShouldResolveRequestHandler()
    {
        _provider.GetService<IRequestHandler<Ping, Pong>>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldResolveNotificationHandlers()
    {
        _provider.GetServices<INotificationHandler<Pinged>>().Count().ShouldBe(4);
    }

    [Fact]
    public void Can_Call_AddMediator_multiple_times()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(new Logger());
        services.AddMediator(cfg =>
        {
            cfg.MediatorImplementationType = typeof(MyCustomMediator);
            cfg.RegisterServicesFromAssemblyContaining(typeof(CustomMediatorTests));
        });
            
        // Call AddMediator again, this should NOT override our custom Questy (With MS DI, last registration wins)
        services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining(typeof(CustomMediatorTests)));

        ServiceProvider provider = services.BuildServiceProvider();
        IMediator mediator = provider.GetRequiredService<IMediator>();
        mediator.GetType().ShouldBe(typeof(MyCustomMediator));
    }
}