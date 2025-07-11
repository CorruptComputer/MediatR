using Questy.NotificationPublishers;
using Questy.Wrappers;
using System.Collections.Concurrent;

namespace Questy;

/// <summary>
///   Default mediator implementation relying on single- and multi instance delegates for resolving handlers.
/// </summary>
/// <remarks>
///   Initializes a new instance of the <see cref="Mediator"/> class.
/// </remarks>
/// <param name="serviceProvider">Service provider. Can be a scoped or root provider</param>
/// <param name="publisher">Notification publisher. Defaults to <see cref="ForeachAwaitPublisher"/>.</param>
public class Mediator(IServiceProvider serviceProvider, INotificationPublisher publisher) : IMediator
{
    private static readonly ConcurrentDictionary<Type, RequestHandlerBase> _requestHandlers = new();
    private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> _notificationHandlers = new();
    private static readonly ConcurrentDictionary<Type, StreamRequestHandlerBase> _streamRequestHandlers = new();

    /// <summary>
    ///   Initializes a new instance of the <see cref="Mediator"/> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider. Can be a scoped or root provider</param>
    public Mediator(IServiceProvider serviceProvider) : this(serviceProvider, new ForeachAwaitPublisher()) { }

    /// <summary>
    ///   Sends a request and returns a response.
    /// </summary>
    /// <typeparam name="TResponse">The type for the response.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        RequestHandlerWrapper<TResponse> handler = (RequestHandlerWrapper<TResponse>)_requestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            Type wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
            object wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
            return (RequestHandlerBase)wrapper;
        });

        return handler.Handle(request, serviceProvider, cancellationToken);
    }

    /// <summary>
    ///   Sends a request without a response.
    /// </summary>
    /// <typeparam name="TRequest">The type for the request.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        RequestHandlerWrapper handler = (RequestHandlerWrapper)_requestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            Type wrapperType = typeof(RequestHandlerWrapperImpl<>).MakeGenericType(requestType);
            object wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
            return (RequestHandlerBase)wrapper;
        });

        return handler.Handle(request, serviceProvider, cancellationToken);
    }

    /// <summary>
    ///   Sends a request and returns a response.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        RequestHandlerBase handler = _requestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            Type wrapperType;

            Type? requestInterfaceType = requestType.GetInterfaces().FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));
            if (requestInterfaceType is null)
            {
                requestInterfaceType = requestType.GetInterfaces().FirstOrDefault(static i => i == typeof(IRequest));
                if (requestInterfaceType is null)
                {
                    throw new ArgumentException($"{requestType.Name} does not implement {nameof(IRequest)}", nameof(request));
                }

                wrapperType = typeof(RequestHandlerWrapperImpl<>).MakeGenericType(requestType);
            }
            else
            {
                Type responseType = requestInterfaceType.GetGenericArguments()[0];
                wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
            }

            object wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper for type {requestType}");
            return (RequestHandlerBase)wrapper;
        });

        // call via dynamic dispatch to avoid calling through reflection for performance reasons
        return handler.Handle(request, serviceProvider, cancellationToken);
    }

    /// <summary>
    ///   Publishes a notification to the appropriate handlers using the default strategy.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification.</typeparam>
    /// <param name="notification">The notification.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        if (notification == null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        return PublishNotification(notification, cancellationToken);
    }

    /// <summary>
    ///   Publishes a notification to the appropriate handlers using the specified strategy.
    /// </summary>
    /// <param name="notification">The notification.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public Task Publish(object notification, CancellationToken cancellationToken = default) =>
        notification switch
        {
            null => throw new ArgumentNullException(nameof(notification)),
            INotification instance => PublishNotification(instance, cancellationToken),
            _ => throw new ArgumentException($"{nameof(notification)} does not implement ${nameof(INotification)}")
        };

    /// <summary>
    ///   Override in a derived class to control how the tasks are awaited. By default the implementation calls the <see cref="INotificationPublisher"/>.
    /// </summary>
    /// <param name="handlerExecutors">Enumerable of tasks representing invoking each notification handler</param>
    /// <param name="notification">The notification being published</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task representing invoking all handlers</returns>
    protected virtual Task PublishCore(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
        => publisher.Publish(handlerExecutors, notification, cancellationToken);

    private Task PublishNotification(INotification notification, CancellationToken cancellationToken = default)
    {
        NotificationHandlerWrapper handler = _notificationHandlers.GetOrAdd(notification.GetType(), static notificationType =>
        {
            Type wrapperType = typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(notificationType);
            object wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper for type {notificationType}");
            return (NotificationHandlerWrapper)wrapper;
        });

        return handler.Handle(notification, serviceProvider, PublishCore, cancellationToken);
    }

    /// <summary>
    ///   Creates a stream request handler for the specified request type.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The stream request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        StreamRequestHandlerWrapper<TResponse> streamHandler = (StreamRequestHandlerWrapper<TResponse>)_streamRequestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            Type wrapperType = typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
            object wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper for type {requestType}");
            return (StreamRequestHandlerBase)wrapper;
        });

        IAsyncEnumerable<TResponse> items = streamHandler.Handle(request, serviceProvider, cancellationToken);

        return items;
    }

    /// <summary>
    ///   Creates a stream request handler for the specified request type.
    /// </summary>
    /// <param name="request">The stream request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        StreamRequestHandlerBase handler = _streamRequestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            Type? requestInterfaceType = requestType.GetInterfaces().FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequest<>));
            if (requestInterfaceType is null)
            {
                throw new ArgumentException($"{requestType.Name} does not implement IStreamRequest<TResponse>", nameof(request));
            }

            Type responseType = requestInterfaceType.GetGenericArguments()[0];
            Type wrapperType = typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
            object wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper for type {requestType}");
            return (StreamRequestHandlerBase)wrapper;
        });

        IAsyncEnumerable<object?> items = handler.Handle(request, serviceProvider, cancellationToken);

        return items;
    }
}
