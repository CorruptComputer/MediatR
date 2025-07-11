using System;
using System.IO;
using System.Threading.Tasks;
using Lamar;
using Questy.Pipeline;

namespace Questy.Examples.Lamar;

class Program
{
    static Task Main(string[] args)
    {
        WrappingWriter writer = new(Console.Out);
        IMediator mediator = BuildMediator(writer);

        return Runner.Run(mediator, writer, "Lamar");
    }

    private static IMediator BuildMediator(WrappingWriter writer)
    {
        Container container = new(cfg =>
        {
            cfg.Scan(scanner =>
            {
                scanner.AssemblyContainingType<Ping>();
                scanner.ConnectImplementationsToTypesClosing(typeof(IRequestHandler<,>));
                scanner.ConnectImplementationsToTypesClosing(typeof(INotificationHandler<>));
                scanner.ConnectImplementationsToTypesClosing(typeof(IRequestExceptionAction<,>));
                scanner.ConnectImplementationsToTypesClosing(typeof(IRequestExceptionHandler<,,>));
            });

            //Pipeline
            cfg.For(typeof(IPipelineBehavior<,>)).Add(typeof(RequestExceptionProcessorBehavior<,>));
            cfg.For(typeof(IPipelineBehavior<,>)).Add(typeof(RequestExceptionActionProcessorBehavior<,>));
            cfg.For(typeof(IPipelineBehavior<,>)).Add(typeof(RequestPreProcessorBehavior<,>));
            cfg.For(typeof(IPipelineBehavior<,>)).Add(typeof(RequestPostProcessorBehavior<,>));
            cfg.For(typeof(IPipelineBehavior<,>)).Add(typeof(GenericPipelineBehavior<,>));
            cfg.For(typeof(IRequestPreProcessor<>)).Add(typeof(GenericRequestPreProcessor<>));
            cfg.For(typeof(IRequestPostProcessor<,>)).Add(typeof(GenericRequestPostProcessor<,>));
            cfg.For(typeof(IRequestPostProcessor<,>)).Add(typeof(ConstrainedRequestPostProcessor<,>));

            //Constrained notification handlers
            cfg.For(typeof(INotificationHandler<>)).Add(typeof(ConstrainedPingedHandler<>));

            // This is the default but let's be explicit. At most we should be container scoped.
            cfg.For<IMediator>().Use<Mediator>().Transient();

            cfg.For<TextWriter>().Use(writer);
        });


        IMediator mediator = container.GetInstance<IMediator>();

        return mediator;
    }
}