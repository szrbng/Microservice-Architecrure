using EventBus.Base.Abstraction;
using EventBus.Base.SubManagers;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.Base.Events
{
    public abstract class BaseEventBus :IEventBus
    {
        public readonly IServiceProvider ServiceProvider;
        public readonly IEventBusSubscriptionManager SubscriptionManager;

        public EventBusConfig eventbusConfig { get; set; }

        public BaseEventBus(EventBusConfig config,IServiceProvider serviceProvider)
        {
            eventbusConfig = config ?? throw new ArgumentNullException(nameof(config));

            ServiceProvider = serviceProvider;
            SubscriptionManager = new InMemorySubscriptionEventBusManager(ProcessEventName);
        }

        public virtual string ProcessEventName(string eventName)
        {
            if(eventbusConfig.DeleteEventPrefix)
                eventName = eventName.TrimStart(eventbusConfig.EventNamePrefix.ToArray());

            if (eventbusConfig.DeleteEventSuffix)
                eventName = eventName.TrimEnd(eventbusConfig.EventNameSuffix.ToArray());

            return eventName;

        }
        public virtual string GetSubName(string eventName)
        {
            return $"{eventbusConfig.SubscriberClientAppName}.{ProcessEventName(eventName)}";
        }

        public virtual void Dispose()
        {
            eventbusConfig = null;
            SubscriptionManager.Clear();
        }

        public async Task<bool> ProcessEvent(string eventName,string message)
        {
            eventName = ProcessEventName(eventName);

            var processed = false;

            if(SubscriptionManager.HasSubscriptiponsForEvent(eventName))
            {
                var subscriptions = SubscriptionManager.GetHandlersForEvents(eventName);

                using(var scope = ServiceProvider.CreateScope())
                {
                    foreach (var subscription in subscriptions)
                    {
                        var handler = ServiceProvider.GetService(subscription.HandleType);
                        if (handler == null) continue;

                        var eventtype = SubscriptionManager.GetEventTypeByName($"{eventbusConfig.EventNamePrefix}{eventName}{eventbusConfig.EventNameSuffix}");
                        var integrationEvent = JsonConvert.DeserializeObject(message, eventtype);


                        var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventtype);
                        await (Task)concreteType.GetMethod("Handle").Invoke(handler,new object[] { integrationEvent });
                    }
                }

                processed = true;
            }

            return processed;
        }

        public abstract void Publish(IntegrationEvent @event);
        
        public abstract void Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;


        public abstract void UnSubscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;
       
    }
}
