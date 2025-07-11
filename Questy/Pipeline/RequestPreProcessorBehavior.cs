namespace Questy.Pipeline;

/// <summary>
///   Behavior for executing all <see cref="IRequestPreProcessor{TRequest}"/> instances before handling a request
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public class RequestPreProcessorBehavior<TRequest, TResponse>(IEnumerable<IRequestPreProcessor<TRequest>> preProcessors) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        foreach (IRequestPreProcessor<TRequest> processor in preProcessors)
        {
            await processor.Process(request, cancellationToken).ConfigureAwait(false);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}