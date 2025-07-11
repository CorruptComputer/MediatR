﻿using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Questy.NotificationPublishers;
using Shouldly;
using Xunit;

namespace Questy.Extensions.Microsoft.DependencyInjection.Tests;

public class NotificationPublisherTests
{
    public class MockPublisher : INotificationPublisher
    {
        public int CallCount { get; set; }

        public async Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
        {
            foreach (NotificationHandlerExecutor handlerExecutor in handlerExecutors)
            {
                await handlerExecutor.HandlerCallback(notification, cancellationToken);
                CallCount++;
            }
        }
    }

    [Fact]
    public void ShouldResolveDefaultPublisher()
    {
        ServiceCollection services = new();
        services.AddSingleton(new Logger());
        services.AddMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining(typeof(CustomMediatorTests));
        });

        ServiceProvider provider = services.BuildServiceProvider();
        IMediator? mediator = provider.GetService<IMediator>();

        mediator.ShouldNotBeNull();

        INotificationPublisher? publisher = provider.GetService<INotificationPublisher>();

        publisher.ShouldNotBeNull();
    }

    [Fact]
    public async Task ShouldSubstitutePublisherInstance()
    {
        MockPublisher publisher = new();
        ServiceCollection services = new();
        services.AddSingleton(new Logger());
        services.AddMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining(typeof(CustomMediatorTests));
            cfg.NotificationPublisher = publisher;
        });

        ServiceProvider provider = services.BuildServiceProvider();
        IMediator? mediator = provider.GetService<IMediator>();

        mediator.ShouldNotBeNull();

        await mediator.Publish(new Pinged());
        
        publisher.CallCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ShouldSubstitutePublisherServiceType()
    {
        ServiceCollection services = new();
        services.AddSingleton(new Logger());
        services.AddMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining(typeof(CustomMediatorTests));
            cfg.NotificationPublisherType = typeof(MockPublisher);
            cfg.Lifetime = ServiceLifetime.Singleton;
        });

        ServiceProvider provider = services.BuildServiceProvider();
        IMediator? mediator = provider.GetService<IMediator>();
        INotificationPublisher? publisher = provider.GetService<INotificationPublisher>();

        mediator.ShouldNotBeNull();
        publisher.ShouldNotBeNull();

        await mediator.Publish(new Pinged());

        MockPublisher mock = publisher.ShouldBeOfType<MockPublisher>();

        mock.CallCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ShouldSubstitutePublisherServiceTypeWithWhenAll()
    {
        ServiceCollection services = new();
        services.AddSingleton(new Logger());
        services.AddMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining(typeof(CustomMediatorTests));
            cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher);
            cfg.Lifetime = ServiceLifetime.Singleton;
        });

        ServiceProvider provider = services.BuildServiceProvider();
        IMediator? mediator = provider.GetService<IMediator>();
        INotificationPublisher? publisher = provider.GetService<INotificationPublisher>();

        mediator.ShouldNotBeNull();
        publisher.ShouldNotBeNull();

        await Should.NotThrowAsync(mediator.Publish(new Pinged()));

        publisher.ShouldBeOfType<TaskWhenAllPublisher>();
    }
}