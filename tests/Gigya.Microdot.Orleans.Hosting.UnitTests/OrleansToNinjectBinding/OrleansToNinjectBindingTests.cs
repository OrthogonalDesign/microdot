﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding;
using Microsoft.Extensions.DependencyInjection;
using Ninject;
using Ninject.Extensions.Factory;
using NUnit.Framework;
using Orleans.Runtime;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.OrleansToNinjectBinding
{
    [TestFixture, Parallelizable(ParallelScope.All)]
    public class OrleansToNinjectBindingTests
    {
        public enum ServiceProviderType
        {
            microdot, microsoft
        }
        internal IServiceProvider CreateServiceProvider(IServiceCollection binding, ServiceProviderType serviceProviderType)
        {
            if (serviceProviderType == ServiceProviderType.microdot)
            {
                IKernel kernel = new MicrodotKernel(new NinjectSettings { ActivationCacheDisabled = true });
                var registerBinding = new Ninject.Host.NinjectOrleansBinding.OrleansToNinjectBinding(kernel);
                registerBinding.ConfigureServices(binding);
                return kernel.Get<IServiceProvider>();
            }
            else
            {
               
                var services = binding.BuildServiceProvider();
              //  var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions() { ValidateScopes = true }); ;
                return services.GetService<IServiceProvider>();
            }
        }


        [TestCase(ServiceProviderType.microdot)]
        [TestCase(ServiceProviderType.microsoft)]
        public void When_request_to_resolve_singleton_multple_times_should_resolve_same_object(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection().AddSingleton<Dependency>();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);
            var object1 = serviceProvider.GetService(typeof(Dependency));
            var object2 = serviceProvider.GetService(typeof(Dependency));
            Assert.AreEqual(object1, object2);
        }


        [TestCase(ServiceProviderType.microdot)]
        [TestCase(ServiceProviderType.microsoft)]
        public void When_request_to_resolve_mutiple_scopeDepency_on_same_Scope_should_resolve_same_object(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection().AddScoped<Dependency>();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);

            var serviceScopeFactory = (IServiceScopeFactory)serviceProvider.GetService(typeof(IServiceScopeFactory));
            var serviceScope = serviceScopeFactory.CreateScope();

            var object1 = serviceScope.ServiceProvider.GetService(typeof(Dependency));
            var object2 = serviceScope.ServiceProvider.GetService(typeof(Dependency));
            Assert.AreEqual(object1, object2);
        }


        [TestCase(ServiceProviderType.microdot)]
        public void When_request_to_resolve_serviceScope_Should_fail(ServiceProviderType serviceProviderType)
        {
            /// ServiceScope should be created by the scope factory alone!!
            var serviceProvider = CreateServiceProvider(new ServiceCollection(), serviceProviderType);
            Assert.Throws<Exception>(() => serviceProvider.GetService(typeof(IServiceScope)));
        }


        [TestCase(ServiceProviderType.microsoft)]
        public void When_request_to_resolve_serviceScope_Should_BeNull(ServiceProviderType serviceProviderType)
        {
            /// ServiceScope should be created by the scope factory alone!!
            var serviceProvider = CreateServiceProvider(new ServiceCollection(), serviceProviderType);
            var scopeFactory = (IServiceScopeFactory)serviceProvider.GetService(typeof(IServiceScopeFactory));

            Assert.IsNull(scopeFactory.CreateScope().ServiceProvider.GetService(typeof(IServiceScope)));
        }


        [Repeat(100)]//Race Condion make sure it heappend
        [TestCase(ServiceProviderType.microdot)]
        [TestCase(ServiceProviderType.microsoft)]
        public void When_request_same_scopeDepency_on_scope_HighParallelem_Should_create_one_object(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection().AddScoped<Dependency>();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);
            var serviceScopeFactory = (IServiceScopeFactory)serviceProvider.GetService(typeof(IServiceScopeFactory));
            var serviceScope = serviceScopeFactory.CreateScope();
            ConcurrentBag<Dependency> dependencies = new ConcurrentBag<Dependency>();
            Parallel.For(0, 100, (i) =>
            {
                var object1 = (Dependency)serviceScope.ServiceProvider.GetService(typeof(Dependency));
                dependencies.Add(object1);
            });

            var groups = dependencies.ToArray().GroupBy(x => x);
            Assert.AreEqual(1, groups.Count());
        }


        [TestCase(ServiceProviderType.microdot)]
        [TestCase(ServiceProviderType.microsoft)]
        public void When_Request_to_Resolve_ScopeDependcy_OnGlobalScope_Should_(ServiceProviderType serviceProviderType)
        {
            /// When reqesting a object register to a scope what is microsoft bhiverr?
            var binding = new ServiceCollection().AddScoped<Dependency>();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);
            var a = serviceProvider.GetService<Dependency>();
            var b = serviceProvider.GetService<Dependency>();

            Assert.AreEqual(a, b);

        }

        // like When_request_ServiceProvider_Sholud_inherit_the_Scope
        [TestCase(ServiceProviderType.microdot)]
        public void When_resolve_factory_on_scope_Should_Resolve_Same_Object(ServiceProviderType serviceProviderType)
        {

            var binding = new ServiceCollection().AddScoped<Dependency>();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);
            var serviceScopeFactory = (IServiceScopeFactory)serviceProvider.GetService(typeof(IServiceScopeFactory));
            var serviceScope = serviceScopeFactory.CreateScope();

            var object1 = serviceScope.ServiceProvider.GetService(typeof(Dependency));
            var factory = (Func<Dependency>)serviceScope.ServiceProvider.GetService(typeof(Func<Dependency>));
            Assert.AreEqual(object1, factory());
        }

        [TestCase(ServiceProviderType.microdot)]
        [TestCase(ServiceProviderType.microsoft)]
        public void When_resolve_innerTransientDependyOnScope__Should_resolve_new_object(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection().AddTransient<DependencyDependedOn_Dependency>().AddTransient<Dependency>();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);

            var continer2 = (DependencyDependedOn_Dependency)serviceProvider.GetService(typeof(DependencyDependedOn_Dependency));
            Assert.IsNotNull(continer2);

            var serviceScopeFactory = (IServiceScopeFactory)serviceProvider.GetService(typeof(IServiceScopeFactory));
            var serviceScope = serviceScopeFactory.CreateScope();

            var object1 = serviceScope.ServiceProvider.GetService(typeof(Dependency));
            Assert.IsNotNull(object1);

            var continer = (DependencyDependedOn_Dependency)serviceScope.ServiceProvider.GetService(typeof(DependencyDependedOn_Dependency));
            Assert.IsNotNull(continer);
            Assert.AreNotEqual(object1, continer.Dependency);
        }

        [TestCase(ServiceProviderType.microdot)]
        [TestCase(ServiceProviderType.microsoft)]
        public void When_resolve_innerScopeDependy_on_scope_Should_resolve_same_object(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection().AddTransient<DependencyDependedOn_Dependency>().AddScoped<Dependency>();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);
            var Dependency1 = (Dependency)serviceProvider.GetService(typeof(Dependency));
            Assert.IsNotNull(Dependency1);

            var continer2 = (DependencyDependedOn_Dependency)serviceProvider.GetService(typeof(DependencyDependedOn_Dependency));
            Assert.IsNotNull(continer2);

            var serviceScopeFactory = (IServiceScopeFactory)serviceProvider.GetService(typeof(IServiceScopeFactory));
            var serviceScope = serviceScopeFactory.CreateScope();

            var object1 = serviceScope.ServiceProvider.GetService(typeof(Dependency));
            Assert.IsNotNull(object1);

            var continer = (DependencyDependedOn_Dependency)serviceScope.ServiceProvider.GetService(typeof(DependencyDependedOn_Dependency));
            Assert.IsNotNull(continer);
            Assert.AreEqual(object1, continer.Dependency);
        }
      
        [TestCase(ServiceProviderType.microsoft)]
        [TestCase(ServiceProviderType.microdot)]
        public void ServiceProvider_Should_Support_IEnumerable_for_Multiple_Binding(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection()
                   .AddSingleton(typeof(IDependency), typeof(Dependency))
                   .AddSingleton(typeof(IDependency), typeof(Dependency2)
                   );

            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);

            var object1 = (IEnumerable<IDependency>)serviceProvider.GetService(typeof(IEnumerable<IDependency>));
            Assert.AreEqual(2, object1.Count());
        }


        [TestCase(ServiceProviderType.microdot)]
        public void KeyedServiceCollection_Should_Support_Multiple_Named_Binding(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection()
                    .AddSingleton<Dependency2>().AddSingleton<Dependency>()
                   .AddSingletonNamedService<IDependency>("A", (s, k) => { return s.GetService<Dependency2>(); })
                   .AddSingletonNamedService<IDependency>("B", (s, k) => { return s.GetService<Dependency>(); });

            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);

            var object1 = (IKeyedServiceCollection<string, IDependency>)serviceProvider.GetService(typeof(IKeyedServiceCollection<string, IDependency>));
            Assert.AreEqual(typeof(Dependency2), object1.GetService(serviceProvider, "A").GetType());
            Assert.AreEqual(typeof(Dependency), object1.GetService(serviceProvider, "B").GetType());
        }


        [TestCase(ServiceProviderType.microdot)]
        public void ServiceProvider_should_support_Func(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection().AddTransient(typeof(Dependency));
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);

            var factory = (Func<Dependency>)serviceProvider.GetService(typeof(Func<Dependency>));

            Assert.AreNotEqual(factory(), factory());
        }


        [TestCase(ServiceProviderType.microsoft)]
        public void ServiceProvider_should_Not_support_Func(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection().AddTransient(typeof(Dependency));
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);

            var factory = (Func<Dependency>)serviceProvider.GetService(typeof(Func<Dependency>));
            Assert.IsNull(factory);
        }


        [TestCase(ServiceProviderType.microsoft)]
        public void When_request_UnbindType_ShouldBeNull(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);

            var factory = serviceProvider.GetService(typeof(DependencyDependedOn_Dependency));
            Assert.IsNull(factory);
        }


        [TestCase(ServiceProviderType.microdot)]
        public void When_request_UnbindType_ShouldNotBeNull(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);

            var factory = serviceProvider.GetService(typeof(DependencyDependedOn_Dependency));
            Assert.IsNotNull(factory);
        }


        [TestCase(ServiceProviderType.microdot)]
        public void When_request_UnbindTypeIntfrace_ShouldThrow(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);

            Assert.Throws<ActivationException>(() => serviceProvider.GetService(typeof(IDependency)));

        }


        [TestCase(ServiceProviderType.microsoft)]
        public void When_request_UnbindTypeIntfrace_ShouldBeNull(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);

            Assert.IsNull(serviceProvider.GetService(typeof(IDependency)));
        }


        [TestCase(ServiceProviderType.microdot)]
        [TestCase(ServiceProviderType.microsoft)]
        public void When_resolve_DependencyScope_on_different_scopes_Should_create_Multiple_object(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection().AddScoped<Dependency>();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);
            var serviceScopeFactory = (IServiceScopeFactory)serviceProvider.GetService(typeof(IServiceScopeFactory));

            //Scope 1
            var serviceScope = serviceScopeFactory.CreateScope();
            var object1 = serviceScope.ServiceProvider.GetService(typeof(Dependency));
            //Scope 2
            var serviceScope2 = serviceScopeFactory.CreateScope();
            var object2 = serviceScope2.ServiceProvider.GetService(typeof(Dependency));

            Assert.AreNotEqual(object1, object2);
        }



        [TestCase(ServiceProviderType.microdot)]
        [TestCase(ServiceProviderType.microsoft)]
        public void When_request_ServiceProvider_Sholud_inherit_the_Scope(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection().AddScoped<Dependency>();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);
            var serviceScopeFactory = (IServiceScopeFactory)serviceProvider.GetService(typeof(IServiceScopeFactory));

            //Scope 1
            var serviceScope = serviceScopeFactory.CreateScope();
            var object1 = serviceScope.ServiceProvider.GetService(typeof(Dependency));

            var serviceProviderNoScope = (IServiceProvider)serviceScope.ServiceProvider.GetService(typeof(IServiceProvider));
            var object2 = serviceProviderNoScope.GetService(typeof(Dependency));
            Assert.AreEqual(object1, object2);
        }



        [TestCase(ServiceProviderType.microdot)]
        [TestCase(ServiceProviderType.microsoft)]
        public void When_disposeing_scope_should_dispose_of_all_scope_dependency(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection().AddScoped<Dependency>().AddScoped<DisposableDependency>();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);
            var serviceScopeFactory = (IServiceScopeFactory)serviceProvider.GetService(typeof(IServiceScopeFactory));
            //Scope 1
            var serviceScope = serviceScopeFactory.CreateScope();
            DisposableDependency object1 = (DisposableDependency)serviceScope.ServiceProvider.GetService(typeof(DisposableDependency));
            serviceScope.Dispose();

            Assert.AreEqual(object1.DisposeCounter, 1);
        }


        /// <remarks>
        /// This test doing some Tweaks to make sure gc is collecting the object event in debug mode
        /// </remarks>
        [TestCase(ServiceProviderType.microdot)]
        [TestCase(ServiceProviderType.microsoft)]
        public void Scope_should_not_be_rooted(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection().AddScoped<Dependency>();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);
            var serviceScopeFactory = (IServiceScopeFactory)serviceProvider.GetService(typeof(IServiceScopeFactory));

            WeakReference<object> holder = null;
            Action notRootByDebuger = () =>
            {
                holder = new WeakReference<object>(serviceScopeFactory.CreateScope());

            };
            notRootByDebuger();
            MakeSomeGarbage();

            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCApproach();
            GC.Collect(2);

            Assert.False(holder.TryGetTarget(out _), "scope object in not rooted, it should be collected");
        }


        [TestCase(ServiceProviderType.microdot)]
        [TestCase(ServiceProviderType.microsoft)]
        public void Scope_Dependency_should_not_be_rootd(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection().AddScoped<Dependency>();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);
            var serviceScopeFactory = (IServiceScopeFactory)serviceProvider.GetService(typeof(IServiceScopeFactory));

            WeakReference<object> holder = null;
            Action notRootByDebuger = () =>
            {
                holder = new WeakReference<object>(serviceScopeFactory.CreateScope().ServiceProvider.GetService(typeof(Dependency)));
            };
            notRootByDebuger();
            MakeSomeGarbage();

            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCApproach();
            GC.Collect(2);


            Assert.False(holder.TryGetTarget(out _), "scope object in not rooted, it should be collected");
        }


        [TestCase(ServiceProviderType.microdot)]
        [TestCase(ServiceProviderType.microsoft)]
        public void Scope_Dependency_should_be_Rooted_To_Scope(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection().AddScoped<Dependency>();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);
            var serviceScopeFactory = (IServiceScopeFactory)serviceProvider.GetService(typeof(IServiceScopeFactory));

            var scope = serviceScopeFactory.CreateScope();
            WeakReference<object> holder = null;
            Action notRootByDebuger = () =>
            {
                holder = new WeakReference<object>(scope.ServiceProvider.GetService(typeof(Dependency)));
            };
            notRootByDebuger();
            MakeSomeGarbage();

            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCApproach();
            GC.Collect(2);

            Assert.True(holder.TryGetTarget(out _), "Dependency is rooted to scoped, it should bo be collected");
            scope.Dispose();
        }


        [TestCase(ServiceProviderType.microdot)]
        [TestCase(ServiceProviderType.microsoft)]
        public void SingletonDependency_should_be_rooted(ServiceProviderType serviceProviderType)
        {
            var binding = new ServiceCollection().AddSingleton<Dependency>();
            var serviceProvider = CreateServiceProvider(binding, serviceProviderType);

            WeakReference<object> holder = null;
            Action notRootByDebuger = () =>
            {
                holder = new WeakReference<object>(serviceProvider.GetService(typeof(Dependency)));
            };
            notRootByDebuger();
            MakeSomeGarbage();

            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCApproach();
            GC.Collect(2);


            Assert.True(holder.TryGetTarget(out _), "Dependency is rooted to scoped, it should bo be collected");
        }


        void MakeSomeGarbage()
        {
            Version vt;

            for (int i = 0; i < 10000; i++)
            {
                // Create objects and release them to fill up memory
                // with unused objects.

                vt = new Version();

            }
        }

        internal interface IDependency { }
        internal class Dependency : IDependency { }
        internal class Dependency2 : IDependency { }

        internal class DependencyDependedOn_Dependency 
        {
            public DependencyDependedOn_Dependency(Dependency dependency)
            {
                Dependency = dependency;
            }

            public Dependency Dependency { get; }
        }

        internal class DisposableDependency : IDisposable
        {
            public int DisposeCounter;
            public void Dispose()
            {
                Interlocked.Increment(ref DisposeCounter);
            }
        }
    }
}