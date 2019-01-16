namespace MediatR.Internal
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class RequestHandlerBase
    {
        protected static THandler GetHandler<THandler>(ServiceFactory factory)
        {
            THandler handler;

            try
            {
                handler = factory.GetInstance<THandler>();
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"Error constructing handler for request of type {typeof(THandler)}. Register your handlers with the container. See the samples in GitHub for examples.",
                    e);
            }

            if (handler == null)
            {
                throw new InvalidOperationException(
                    $"Handler was not found for request of type {typeof(THandler)}. Register your handlers with the container. See the samples in GitHub for examples.");
            }

            return handler;
        }
    }

    internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerBase
    {
        public abstract Task<TResponse> Handle(IRequest<TResponse> request, CancellationToken cancellationToken,
            ServiceFactory serviceFactory);
    }

    internal class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
        where TRequest : IRequest<TResponse>
    {

        public override Task<TResponse> Handle(IRequest<TResponse> request, CancellationToken cancellationToken,
            ServiceFactory serviceFactory)
        {
            //局部函数&&表达式形式的成员函数
            //这一步的目的，是将一个类型函数对象转换为局部函数，方便后续调用。
            //Task<TResponse> Handler() => GetHandler<IRequestHandler<TRequest, TResponse>>(serviceFactory).Handle((TRequest)request, cancellationToken);
            //相当于下面的写法：
            Task<TResponse> Handler()
            {
                return GetHandler<IRequestHandler<TRequest, TResponse>>(serviceFactory) //从IOC容器获取该请求对应的请求处理器
                    .Handle((TRequest) request, cancellationToken); //调用Handle方法
            }

            //将函数调用转为委托
            RequestHandlerDelegate<TResponse> handler = () =>
                GetHandler<IRequestHandler<TRequest, TResponse>>(serviceFactory)
                    .Handle((TRequest) request, cancellationToken);

            Func<RequestHandlerDelegate<TResponse>, IPipelineBehavior<TRequest, TResponse>,
                RequestHandlerDelegate<TResponse>> combineFunc = (delegater, behavior) =>
            {
                //将类型函数对象转换为委托
                RequestHandlerDelegate<TResponse> combineDelegate =
                    () => behavior.Handle((TRequest) request, cancellationToken, delegater);
                //相当于下面的写法
                //RequestHandlerDelegate<TResponse> combineDelegate = delegate { return behavior.Handle((TRequest)request, cancellationToken, delegater); };

                return combineDelegate;
            };


            return serviceFactory
                .GetInstances<IPipelineBehavior<TRequest, TResponse>>() //从IOC容器中获取所有的IPipelineBehavior的实现，用于构造请求管道
                .Reverse() //集合反转
                .Aggregate(
                    (RequestHandlerDelegate<TResponse>) Handler, //将局部函数强制转换为委托，等价于handler
                    (next, pipeline) =>
                        () => pipeline.Handle((TRequest) request, cancellationToken,
                            next) ////这一步是借助委托构造函数链，返回的是一个委托，等价于combineDelegate
                )//累加操作返回的是一个由委托构造而成的函数链
                (); //加上()进行委托调用
        }
    }
}
