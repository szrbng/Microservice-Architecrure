using EventBus.Base;
using EventBus.Base.Events;
using Newtonsoft.Json;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EventBus.RabbitMQ
{
    public class EvetBusRabbitMQ : BaseEventBus
    {
        RabbitMQPersistentConnection persistentConnection;
        private readonly IConnectionFactory connectionFactory;
        private readonly IModel consumerChannel;

        public EvetBusRabbitMQ(EventBusConfig config, IServiceProvider serviceProvider) : base(config, serviceProvider)
        {
            if(config.Connection != null)
            {
                var conJson = JsonConvert.SerializeObject(config.Connection, new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                });

                connectionFactory = JsonConvert.DeserializeObject<ConnectionFactory>(conJson);
            }
            else
                connectionFactory = new ConnectionFactory();

            persistentConnection = new RabbitMQPersistentConnection(connectionFactory,config.ConnectionRetryCount);

            consumerChannel = CreateConsumerChannel();

            SubscriptionManager.OnEventRemoved += SubscriptionManager_OnEventRemoved;
        }

        private void SubscriptionManager_OnEventRemoved(object? sender, string eventName) // kuyruğu silmek yerine dinlemeyi bırakıyoruz.
        {
           eventName = ProcessEventName(eventName);

            if (!persistentConnection.IsConnected)
            {
                persistentConnection.TryConnect();
            }

            consumerChannel.QueueUnbind(queue:eventName, exchange: eventbusConfig.DefaultTopicName, routingKey: eventName);

            if (SubscriptionManager.IsEmpty)
            {
                consumerChannel.Close();
            }

        }

        public override void Publish(IntegrationEvent @event)
        {
            if (!persistentConnection.IsConnected)
            {
                persistentConnection.TryConnect();
            }

            var policy = Policy.Handle<BrokerUnreachableException>() //bağlantı mekanisması
                 .Or<SocketException>()
                 .WaitAndRetry(eventbusConfig.ConnectionRetryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                 {
                     //hata var ise  logger.error yazılabilir
                 }
                 );

            var eventName = @event.GetType().Name;
            eventName = ProcessEventName(eventName);

            consumerChannel.ExchangeDeclare(exchange: eventbusConfig.DefaultTopicName, type: "direct");

            var message = JsonConvert.SerializeObject(@event);
            var body = Encoding.UTF8.GetBytes(message);

            policy.Execute(() =>
            {
                var properties = consumerChannel.CreateBasicProperties();
                properties.DeliveryMode = 2; //persistent 

                consumerChannel.QueueDeclare(queue: GetSubName(eventName), durable: true, exclusive: false, autoDelete: false, arguments: null);

                consumerChannel.BasicPublish(exchange: eventbusConfig.DefaultTopicName, routingKey: eventName, mandatory: true, basicProperties: properties, body: body);
            });


        }

        public override void Subscribe<T, TH>()
        {
            var eventName = typeof(T).Name;
            eventName = ProcessEventName(eventName);

            if(!SubscriptionManager.HasSubscriptiponsForEvent(eventName))
            {
                if (!persistentConnection.IsConnected)
                {
                    persistentConnection.TryConnect();
                }

                consumerChannel.QueueDeclare(queue:GetSubName(eventName),durable:true,exclusive:false,autoDelete:false,arguments:null);

                consumerChannel.QueueBind(queue: GetSubName(eventName), exchange: eventbusConfig.DefaultTopicName, routingKey: eventName);
            }

            SubscriptionManager.AddSubscription<T,TH>();

            StartBasicConsume(eventName);
        }

        public override void UnSubscribe<T, TH>()
        {
            SubscriptionManager.RemoveSubscription<T, TH>();
        }

        private IModel CreateConsumerChannel()
        {
            if(!persistentConnection.IsConnected)
            {
                persistentConnection.TryConnect();
            }

            var channel = persistentConnection.CreateModel();

            channel.ExchangeDeclare(exchange: eventbusConfig.DefaultTopicName, type: "direct");

            return channel;
        }

        private void StartBasicConsume(string eventName)
        {
            if(consumerChannel != null)
            {
                var consumer = new EventingBasicConsumer(consumerChannel);

                consumer.Received += Consumer_Received;

                consumerChannel.BasicConsume(queue: GetSubName(eventName), autoAck: false, consumer: consumer);
            }
        }

        private async void Consumer_Received(object? sender, BasicDeliverEventArgs e)
        {
            var eventName = e.RoutingKey;

            eventName = ProcessEventName(eventName);

            var message = Encoding.UTF8.GetString(e.Body.Span);

            try
            {
                await ProcessEvent(eventName, message);
            }
            catch (Exception ex)
            {

            }

            consumerChannel.BasicAck(e.DeliveryTag, false);
        }
    }
}
