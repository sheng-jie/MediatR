![](https://upload-images.jianshu.io/upload_images/2799767-81f47b8d28199687.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)


# 引言
首先不用查字典了，词典查无此词。猜测是作者笔误将**Mediator**写成MediatR了。废话少说，转入正题。

先来简单了解下这个开源项目[MediatR](https://github.com/jbogard/MediatR)（作者Jimmy Bogard，也是开源项目AutoMapper的创建者，在此表示膜拜）：

>Simple mediator implementation in .NET. In-process messaging with no dependencies. Supports request/response, commands, queries, notifications and events, synchronous and async with intelligent dispatching via C# generic variance.
*.NET中的简单中介者模式实现，一种进程内消息传递机制（无其他外部依赖）。 支持以同步或异步的形式进行请求/响应，命令，查询，通知和事件的消息传递，并通过C#泛型支持消息的智能调度。*

如上所述，其核心是一个中介者模式的.NET实现，其目的是消息发送和消息处理的解耦。它支持以单播和多播形式使用同步或异步的模式来发布消息，创建和侦听事件。

# 中介者模式
既然是对中介者模式的一种实现，那么我们就有必要简要介绍下中介者这个设计模式，以便后续展开。
![中介者模式类图](https://upload-images.jianshu.io/upload_images/2799767-6165cec43764d9cd.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

>中介者模式：用一个中介对象封装一系列的对象交互，中介者使各对象不需要显示地相互作用，从而使耦合松散，而且可以独立地改变它们之间的交互。

看上面的官方定义可能还是有点绕，那么下面这张图应该能帮助你对中介者模式有个直观了解。
![](https://upload-images.jianshu.io/upload_images/2799767-306109184e4beaa6.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

使用中介模式，对象之间的交互将封装在中介对象中。对象不再直接相互交互（解耦），而是通过中介进行交互。这减少了对象之间的依赖性，从而减少了耦合。

那其优缺点也在图中很容易看出：
>**优点**：中介者模式的优点就是减少类间的依赖，把原有的一对多的依赖变成了一对一的依赖，同事类只依赖中介者，减少了依赖，当然同时也降低了类间的耦合
**缺点**：中介者模式的缺点就是中介者会膨胀得很大，而且逻辑复杂，原本N个对象直接的相互依赖关系转换为中介者和同事类的依赖关系，同事类越多，中介者的逻辑就越复杂。

# Hello MeidatR
在开始之前，我们先来了解下其基本用法。
## 单播消息传输
单播消息传输，也就是一对一的消息传递，一个消息对应一个消息处理。其通过`IRequest`来抽象单播消息，用`IRequestHandler`进行消息处理。
```
//构建 消息请求
public class Ping : IRequest<string> { }
//构建 消息处理
public class PingHandler : IRequestHandler<Ping, string> {
    public Task<string> Handle(Ping request, CancellationToken cancellationToken) {
        return Task.FromResult("Pong");
    }
}
//发送 请求
var response = await mediator.Send(new Ping());
Debug.WriteLine(response); // "Pong"

```

## 多播消息传输
多播消息传输，也就是一对多的消息传递，一个消息对应多个消息处理。其通过`INotification`来抽象多播消息，对应的消息处理类型为`INotificationHandler`。
```
//构建 通知消息
public class Ping : INotification { }
//构建 消息处理器1
public class Pong1 : INotificationHandler<Ping> {
    public Task Handle(Ping notification, CancellationToken cancellationToken) {
        Debug.WriteLine("Pong 1");
        return Task.CompletedTask;
    }
}
//构建 消息处理器2
public class Pong2 : INotificationHandler<Ping> {
    public Task Handle(Ping notification, CancellationToken cancellationToken) {
        Debug.WriteLine("Pong 2");
        return Task.CompletedTask;
    }
}

//发布消息
await mediator.Publish(new Ping());
```

# 源码解析
对MediatR有了基本认识后，我们来看看源码，研究下其如何实现的。

![类图](https://upload-images.jianshu.io/upload_images/2799767-00b8410a546d62e4.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

从代码图中我们可以看到其核心的对象主要包括：
1. IRequest Vs IRequestHandler
2. INotification Vs INoticifaitonHandler
3. IMediator Vs Mediator
4. Unit
5. IPipelineBehavior

## IRequest Vs IRequestHandler
其中`IRequest`和`INotification`分别对应单播和多播消息的抽象。
对于单播消息可以决定是否需要返回值选用不同的接口：
* IRequest<T> - 有返回值
* IRequest - 无返回值

这里就不得不提到其中巧妙的设计，通过引入结构类型`Unit`来代表无返回的情况。
```
/// <summary>
/// 代表无需返回值的请求
/// </summary>
public interface IRequest : IRequest<Unit> { }

/// <summary>
/// 代表有返回值的请求
/// </summary>
/// <typeparam name="TResponse">Response type</typeparam>
public interface IRequest<out TResponse> : IBaseRequest { }

/// <summary>
/// Allows for generic type constraints of objects implementing IRequest or IRequest{TResponse}
/// </summary>
public interface IBaseRequest { }
```

同样对于`IRequestHandler`也是通过结构类型`Unit`来处理不需要返回值的情况。

```
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest<Unit>
{
}
```

从上面我们可以看出定义了一个方法名为`Handle`返回值为`Task`的包装类型，而因此赋予了其具有以同步和异步的方式进行消息处理的能力。我们再看一下其以异步方式进行消息处理（无返回值）的默认实现`AsyncRequestHandler`：
```
public abstract class AsyncRequestHandler<TRequest> : IRequestHandler<TRequest>
    where TRequest : IRequest
{
    async Task<Unit> IRequestHandler<TRequest, Unit>.Handle(TRequest request, CancellationToken cancellationToken)
    {
        await Handle(request, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }

    protected abstract Task Handle(TRequest request, CancellationToken cancellationToken);
}
```
从上面的代码来看，我们很容易看出这是装饰模式的实现方式，是不是很巧妙的解决了无需返回值的场景。

最后我们来看下结构类型`Unit`的定义：
```
public struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
    public static readonly Unit Value = new Unit();

    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);
    // some other code
}
```

## IMediator Vs Mediator
![MediatR 类图](https://upload-images.jianshu.io/upload_images/2799767-19d4eebd2a6dda89.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

`IMediator`主要定义了两个方法`Send`和`Publish`，分别用于发送消息和发布通知。其默认实现Mediator中定义了两个集合，分别用来保存请求与请求处理的映射关系。
```
//Mediator.cs
//保存request和requesthandler的映射关系，1对1。
private static readonly ConcurrentDictionary<Type, object> _requestHandlers = new ConcurrentDictionary<Type, object>();
//保存notification与notificationhandler的映射关系，
private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> _notificationHandlers = new ConcurrentDictionary<Type, NotificationHandlerWrapper>();
```
这里面其又引入了两个包装类：`RequestHandlerWrapper`和`NotificationHandlerWrapper`。这两个包装类的作用就是用来传递`ServiceFactory`委托进行依赖解析。

所以说`Mediator`借助`public delegate object ServiceFactory(Type serviceType);`完成对Ioc容器的一层抽象。这样就可以对接任意你喜欢用的Ioc容器，比如：Autofac、Windsor或ASP.NET Core默认的Ioc容器，只需要在注册`IMediator`时指定`ServiceFactory`类型的委托即可，比如ASP.NET Core中的做法：
![ASP.NET Core注册IMediatr](https://upload-images.jianshu.io/upload_images/2799767-609f3008b14751ec.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

*在使用ASP.NET Core提供的原生Ioc容器有些问题：[Service registration crashes when registering generic handlers](https://github.com/jbogard/MediatR.Extensions.Microsoft.DependencyInjection/issues/12)*

## IPipelineBehavior

![处理管道](https://upload-images.jianshu.io/upload_images/2799767-42380e9ad9028a7e.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

MeidatR支持按需配置请求管道进行消息处理。即支持在请求处理前和请求处理后添加额外行为。仅需实现以下两个接口，并注册到Ioc容器即可。
* IRequestPreProcessor<in TRequest> 请求处理前接口
* IRequestPostProcessor<in TRequest, in TResponse> 请求处理后接口

其中`IPipelineBehavior`的默认实现：`RequestPreProcessorBehavior`和`RequestPostProcessorBehavior`分别用来处理所有实现`IRequestPreProcessor`和`IRequestPostProcessor`接口定义的管道行为。

而处理管道是如何构建的呢？我们来看下`RequestHandlerWrapperImpl`的具体实现：
```
internal class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<TResponse> Handle(IRequest<TResponse> request, CancellationToken cancellationToken,
        ServiceFactory serviceFactory)
    {
        Task<TResponse> Handler() => GetHandler<IRequestHandler<TRequest, TResponse>>(serviceFactory).Handle((TRequest) request, cancellationToken);

        return serviceFactory
            .GetInstances<IPipelineBehavior<TRequest, TResponse>>()
            .Reverse()
            .Aggregate((RequestHandlerDelegate<TResponse>) Handler, (next, pipeline) => () => pipeline.Handle((TRequest)request, cancellationToken, next))();
    }
}
```
就这样一个简单的函数，涉及的知识点还真不少，说实话我花了不少时间来理清这个逻辑。
那都涉及到哪些知识点呢？我们一个一个的来理一理。
1. C# 7.0的新特性 - 局部函数
2. C# 6.0的新特性 - 表达式形式的成员函数
3. Linq高阶函数 - `Aggregate`
4. 匿名委托
5. 构造委托函数链

关于第1、2个知识点，请看下面这段代码：
```
public delegate int SumDelegate();//定义委托
public static void Main()
{
    //局部函数(在函数内部定义函数)
    //表达式形式的成员函数， 相当于 int Sum() { return 1 + 2;}
    int Sum() => 1 + 2;

    var sumDelegate = (SumDelegate)Sum;//转换为委托
    Console.WriteLine(sumDelegate());//委托调用，输出：3
}
```
再看第4个知识点，匿名委托：
```
public delegate int SumDelegate();

SumDelegate delegater1 = delegate(){ return 1+2; }
//也相当于
SumDelegate delegater2 => 1+2;
```

下面再来介绍一下`Aggregate`这个Linq高阶函数。`Aggregate`是对一个集合序列进行累加操作，通过指定初始值，累加函数，以及结果处理函数完成计算。

函数定义：
```
public static TResult Aggregate<TSource,TAccumulate,TResult>
(this IEnumerable<TSource> source, 
TAccumulate seed, 
Func<TAccumulate,TSource,TAccumulate> func, 
Func<TAccumulate,TResult> resultSelector);
```

根据函数定义我们来写个简单的demo：

```
var nums = Enumerable.Range(2, 3);//[2,3,4]
// 计算1到5的累加之和，再将结果乘以2
var sum = nums.Aggregate(1, (total, next) => total + next, result => result * 2);// 相当于 (((1+2)+3)+4)*2=20
Console.WriteLine(sum);//20
```
和函数参数进行一一对应：
1. seed : 1
2. Func<TAccumulate,TSource,TAccumulate> func : (total, next) => total + next
3. Func<TAccumulate,TResult> resultSelector : result => result * 2


基于上面的认识，我们再来回过头梳理一下`RequestHandlerWrapperImpl`。
其主要是借助委托：`public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();`来构造委托函数链来构建处理管道。

对`Aggregate`函数了解后，我们就不难理解处理管道的构建了。请看下图中的代码解读：

![请求处理管道代码解读](https://upload-images.jianshu.io/upload_images/2799767-4f2985f53e955b6b.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

![构建流程解析](https://upload-images.jianshu.io/upload_images/2799767-ea1971598e09240a.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

那如何保证先执行`IRequestPreProcessor`再执行`IRequestPostProcessor`呢？
就是在注册到Ioc容器时必须保证顺序，先注册`IRequestPreProcessor`再注册`IRequestPostProcessor`。（这一点很重要！！！）

*看到这里有没有想到ASP.NET Core中请求管道中中间件的构建呢？是不是很像俄罗斯套娃？先由内而外构建管道，再由外而内执行！*

至此，MediatR的实现思路算是理清了。

# 应用场景
如文章开头提到：MediatR是一种进程内消息传递机制。 支持以同步或异步的形式进行请求/响应，命令，查询，通知和事件的消息传递，并通过C#泛型支持消息的智能调度。

那么我们就应该明白，其核心是**消息的解耦**。因为我们几乎都是在与消息打交道，那因此它的应用场景就很广泛，比如我们可以基于MediatR实现CQRS、EventBus等。

另外，还有一种应用场景：我们知道借助依赖注入的好处是，就是解除依赖，但我们又不得不思考一个问题，随着业务逻辑复杂度的增加，构造函数可能要注入更多的服务，当注入的依赖太多时，其会导致构造函数膨胀。比如：
```
public DashboardController(
    ICustomerRepository customerRepository,
    IOrderService orderService,
    ICustomerHistoryRepository historyRepository,
    IOrderRepository orderRepository,
    IProductRespoitory productRespoitory,
    IRelatedProductsRepository relatedProductsRepository,
    ISupportService supportService,
    ILog logger
    )  
```
如果借助`MediatR`进行改造，也许仅需注入`IMediatR`就可以了。
```
public DashboardController(IMediatR mediatr)  
```

# 总结
看到这里，也许你应该明白MediatR实质上并不是严格意义上的中介者模式实现，我更倾向于其是基于Ioc容器的一层抽象，根据请求定位相应的请求处理器进行消息处理，也就是服务定位。
那到这里似乎也恍然大悟MediatR这个笔误可能是有意为之了。序员，你怎么看？

> 参考资料：
[CQRS/MediatR implementation patterns](https://lostechies.com/jimmybogard/2016/10/27/cqrsmediatr-implementation-patterns/)

[MediatR when and why I should use it? vs 2017 webapi](https://stackoverflow.com/questions/50663501/mediatr-when-and-why-i-should-use-it-vs-2017-webapi)

[ABP CQRS 实现案例:基于 MediatR 实现](https://www.jianshu.com/p/c28500ec9439?tdsourcetag=s_pctim_aiomsg)
