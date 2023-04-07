using EventBus.AzureServiceBus;
using EventBus.Base;
using EventBus.Base.Abstraction;
using EventBus.RabbitMQ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static EventBus.Base.EventBusConfig;

namespace EventBus.Factory
{
    public class EventBusFactory
    {
        public static IEventBus Create(EventBusConfig config,IServiceProvider serviceProvider)
        {
            //switch (eventBusConfig.EventBusTyped)
            //{
            //    case EventBusType.RabbitMQ:
            //        break;
            //    case EventBusType.AzureServisBus:
            //        break;
            //    case EventBusType.ApaacheKafka:
            //        break;
            //    default:
            //        break;
            //}

            return config.EventBusTyped switch
            {
                EventBusType.AzureServisBus => new EventBusServiceBus(config, serviceProvider),
                _ => new EvetBusRabbitMQ(config, serviceProvider)
            };
        }
    }
}
