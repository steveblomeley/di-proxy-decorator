using SimpleInjector;
using SimpleInjector.Lifestyles;
using System;

namespace DiProxyDecorator
{
    class Program
    {
        // Main method
        // - Configure our DI container
        // - Obtain an instance of IService from our container and display its type (which
        //   should be the type of the last registered Decorator for IService)
        // - Invoke methods on the service to confirm that each method invocation actually
        //   creates and disposes a new instance of the underlying "ConcreteService" type
        static void Main(string[] args)
        {
            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            container.Register<IService, ConcreteService>(Lifestyle.Scoped);
            container.RegisterDecorator<IService, ProxyService>(Lifestyle.Singleton);
            container.Verify();

            var service = container.GetInstance<IService>();

            Console.WriteLine($"Obtained an IService of type {service.GetType()}\n\n");

            var msg = "Hi!";
            Console.WriteLine($"{msg} can be mangled into {service.Mangle(msg)}");
            service.Handle("Each time we invoke a method on the service...");
            service.Handle("...we expect to see a new instance of the service created");
        }
    }

    // The abstract type that our example uses has 2 methods - one returning void, the 
    // other int. Notice that the abstract type is not marked as IDisposable
    public interface IService
    {
        void Handle(string msg);
        int Mangle(string msg);
    }

    // The concrete type that implements our business logic - notice that it contains
    // no references to factories or DI Containers. But we presume that this IDisposable
    // type should be short-lived, and disposed promptly
    public class ConcreteService : IService, IDisposable
    {
        private static int nextId = 1;
        private readonly int id = nextId++;

        public void Dispose()
            => Console.WriteLine($"Disposing service with id: {id.GetHashCode()}");

        public void Handle(string msg)
            => Console.WriteLine($"Message from service id {id} is : {msg}");

        public int Mangle(string msg)
            => msg.GetHashCode();
    }

    // The proxy decorator for our concrete class
    // This contains no reference to our concrete class - It's use depends on the correct
    // container configuration (i.e. this type must be registered as a decorator of IService
    // AFTER the concrete class has been registered as an implementation of IService - that
    // way, SimpleInjector knows to inject a Func<ConcreteService> where the constructor
    // requests a Func<IService>)
    public class ProxyService : ProxyServiceBase<IService>, IService
    {
        public ProxyService(Container container, Func<IService> factory) : base(container, factory)
        {
        }

        public void Handle(string msg)
            => InvokeMethodWithinAsyncScope(service => service.Handle(msg));

        public int Mangle(string msg)
            => InvokeMethodWithinAsyncScope(service => service.Mangle(msg));
    }
    
    // The generic proxy class encapsulates the repeated boilerplate code of creating a new
    // scope, obtaining a new service instance from the factory, and executing the requested
    // method on that service instance. This keeps our concrete proxy decorator classes a 
    // bit cleaner, as they simply need to pass on the constructor arguments, and wrap each
    // invoked method in a call to "InvokeMethodWithinAsyncScope"
    public abstract class ProxyServiceBase<TService>
    {
        private readonly Container _container;
        private readonly Func<TService> _serviceFactory;

        public ProxyServiceBase(Container container, Func<TService> serviceFactory)
        {
            _container = container;
            _serviceFactory = serviceFactory;
        }

        public void InvokeMethodWithinAsyncScope(Action<TService> invokeMethod)
        {
            using (AsyncScopedLifestyle.BeginScope(_container))
            {
                var service = _serviceFactory();
                invokeMethod(service);
            }
        }

        public TReturn InvokeMethodWithinAsyncScope<TReturn>(Func<TService, TReturn> invokeMethod)
        {
            using (AsyncScopedLifestyle.BeginScope(_container))
            {
                var service = _serviceFactory();
                return invokeMethod(service);
            }
        }
    }
}