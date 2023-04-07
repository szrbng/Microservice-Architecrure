using EventBus.Base;
using EventBus.Base.Events;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace EventBus.AzureServiceBus
{
    public class EventBusServiceBus : BaseEventBus
    {
        private ITopicClient _topicClient;
        private ManagementClient _managementClient;
        private ILogger logger;
        public EventBusServiceBus(EventBusConfig config, IServiceProvider serviceProvider) : base(config, serviceProvider)
        {
            logger = serviceProvider.GetService(typeof(ILogger<EventBusServiceBus>)) as ILogger<EventBusServiceBus>;
            _managementClient = new ManagementClient(config.EventBusConnectionString);
            _topicClient = createTopicClient();

        }

        private ITopicClient createTopicClient()
        {
            if(_topicClient == null || _topicClient.IsClosedOrClosing)
            {
                _topicClient = new TopicClient(eventbusConfig.EventBusConnectionString, eventbusConfig.DefaultTopicName,RetryPolicy.Default);
            }


            //create işlemi yaparken ilgili topic varmı yok mu kontrol et
           if(!_managementClient.TopicExistsAsync(eventbusConfig.DefaultTopicName).GetAwaiter().GetResult())
                 _managementClient.CreateTopicAsync(eventbusConfig.DefaultTopicName).GetAwaiter().GetResult();

           return _topicClient;
          
        }

        public override void Publish(IntegrationEvent @event)  //mesajı alıp azure servis bus a gönderecek
        {
            var eventName = @event.GetType().Name; // örnek ordercreatedIntegration 

            eventName = ProcessEventName(eventName); //ordercreated

           var eventStr =  JsonConvert.SerializeObject(@event);
           var bodyAarray = Encoding.UTF8.GetBytes(eventStr);

            var message = new Message()
            {
                MessageId = Guid.NewGuid().ToString(),
                Body = bodyAarray,
                Label = eventName
            };

            _topicClient.SendAsync(message).GetAwaiter().GetResult();
        }

        public override void Subscribe<T, TH>() // var olan EVENTİ DİNLEMEK
        {
            var eventName = typeof(T).Name;
            eventName = ProcessEventName(eventName);

            if (!SubscriptionManager.HasSubscriptiponsForEvent(eventName)) //event daha önce subscribe edilmemiş ise
            {
                var subscriptionClient = CreateSubscriptionClientIfNotExist(eventName);

                RegisterSubscriptionClientMessageHandler(subscriptionClient);
            }

            logger.LogInformation("Subscribing to event {EventName}", eventName,typeof(TH).Name);

            SubscriptionManager.AddSubscription<T, TH>();
        }

        public override void UnSubscribe<T, TH>() //var olan  eventi dinleme
        {
            var eventName = typeof(T).Name;

            try
            {
                var subClient = CreateSubscriptionClient(eventName);

                subClient.RemoveRuleAsync(eventName).GetAwaiter().GetResult();
            }
            catch(MessagingEntityNotFoundException)
            {
                logger.LogWarning("The messaging entity {eventName} could not be found", eventName);
            }

            logger.LogInformation("UnSubscribing from event {EventName}",eventName, typeof(TH).Name);

            SubscriptionManager.RemoveSubscription<T, TH>();
        }

        private void RegisterSubscriptionClientMessageHandler(ISubscriptionClient subscriptionClient)
        {
            subscriptionClient.RegisterMessageHandler(
                async (message, token) =>
                {
                    var eventName = $"{message.Label}";
                    var messageData = Encoding.UTF8.GetString(message.Body);

                    if (await ProcessEvent(ProcessEventName(eventName), messageData))
                    {
                        await subscriptionClient.CompleteAsync(message.SystemProperties.LockToken);

                    }

                },
                new MessageHandlerOptions(ExceptionReceivedHandler) { MaxConcurrentCalls = 10, AutoComplete = false });

                
        }

        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs arg)
        {
           var ex = arg.Exception;

            var context = arg.ExceptionReceivedContext;

            logger.LogError(ex,"ERROR handling message : {ExceptionMessage} - Context: (@ExceptionContext)",ex.Message,context);

            return Task.CompletedTask;
        }

        private SubscriptionClient CreateSubscriptionClient(string eventName)
        {
            return new SubscriptionClient(eventbusConfig.EventBusConnectionString,eventbusConfig.DefaultTopicName,GetSubName(eventName));
        }

        private ISubscriptionClient CreateSubscriptionClientIfNotExist(string eventName)
        {
           var subClient = CreateSubscriptionClient(eventName);

           var exist =  _managementClient.SubscriptionExistsAsync(eventbusConfig.DefaultTopicName,GetSubName(eventName)).GetAwaiter().GetResult();

            if(!exist)
            {
                _managementClient.CreateSubscriptionAsync(eventbusConfig.DefaultTopicName, GetSubName(eventName)).GetAwaiter().GetResult();
                RemoveDefaultRule(subClient);
                
            }
            CreateRuleIfNotExist(ProcessEventName(eventName), subClient);

            return subClient;
        }

        private void CreateRuleIfNotExist(string eventName,ISubscriptionClient subscriptionClient) 
        {
            bool ruleExist;

            try
            {
                var rule = _managementClient.GetRuleAsync(eventbusConfig.DefaultTopicName, eventName, eventName).GetAwaiter().GetResult();
                ruleExist = rule != null;
            }
            catch (MessagingEntityNotFoundException) // hata var ise rule yok demeektir
            {
                ruleExist = false;
            }

            if(!ruleExist)
            {
                subscriptionClient.AddRuleAsync(new RuleDescription
                {
                    Filter = new CorrelationFilter { Label = eventName },
                    Name = eventName
                }).GetAwaiter().GetResult();
            }
        }

        private void RemoveDefaultRule(SubscriptionClient subscriptionClient)
        {
            try
            {
                subscriptionClient.RemoveRuleAsync(RuleDescription.DefaultRuleName).GetAwaiter().GetResult();

            }
            catch (MessagingEntityNotFoundException)
            {
                logger.LogWarning("The messaging entity {DefaultRuleName} Could Not Be Found.", RuleDescription.DefaultRuleName);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            _topicClient.CloseAsync().GetAwaiter().GetResult();
            _managementClient.CloseAsync().GetAwaiter().GetResult();
            _topicClient = null;
            _managementClient = null;
            

        }
    }
}
