using System;
using System.Linq;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.Testing.Service;
using NUnit.Framework;
using System.Threading.Tasks;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Orleans.Hosting.Events;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Ninject;

namespace Gigya.Common.OrleansInfra.FunctionalTests.Events
{
    [TestFixture, Parallelizable(ParallelScope.Fixtures)]
    public class GrainCallEventTests
    {
        private const int REPEAT = 1;

        private SpyEventPublisher _flumeQueue;
        private ICalculatorService _serviceProxy;
        private ServiceTester<CalculatorServiceHost> _serviceTester;
        readonly TracingContextNoneOrleans _tracingContext = new TracingContextNoneOrleans();

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            _serviceTester = new ServiceTester<CalculatorServiceHost>(k=>k.Rebind<TracingContext>().ToConstant(_tracingContext));
            
            _serviceProxy = _serviceTester.GetServiceProxy<ICalculatorService>();
            
            _flumeQueue = (SpyEventPublisher)_serviceTester.Host.Kernel.Get<IEventPublisher>();
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            _serviceTester?.Dispose();
        }

        [Test]
        [Repeat(REPEAT)]
        public async Task SingleGrainCall_CallSucceeds_PublishesEvent()
        {
            _flumeQueue.Clear();

            var requestId = nameof(SingleGrainCall_CallSucceeds_PublishesEvent) + Guid.NewGuid();
            
            _tracingContext.SetRequestID(requestId);
            _tracingContext.TryGetRequestID();

            await _serviceProxy.Add(5, 3);


            var events = _flumeQueue.Events;
            var grainReq = events.Where(r => r.EventType == "grainReq")
                .Select(r => (GrainCallEvent)r)
                .Single(r => r.TargetType == typeof(ICalculatorWorkerGrain).FullName);
            
            Assert.AreEqual("Add", grainReq.TargetMethod);
            Assert.AreEqual(requestId, grainReq.RequestId);
        }
    }
}