﻿using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.RabbitMQ
{
    public class RabbitMQPersistentConnection : IDisposable
    {
        private readonly IConnectionFactory _connectionFactory;
        private readonly int retryCount;
        private IConnection connection;
        private object _lock = new object();
        private bool _disposed;

        public bool IsConnected => connection != null && connection.IsOpen;

        public RabbitMQPersistentConnection(IConnectionFactory connectionFactory,int retryCount)
        {
            _connectionFactory = connectionFactory;
            this.retryCount = retryCount;
        }

        public IModel CreateModel()
        {
            return connection.CreateModel();
        }
        public void Dispose()
        {
            connection.Dispose();
        }

        public bool TryConnect()
        {
            lock (_lock)
            {
                var policy = Policy.Handle<SocketException>()
                    .Or<BrokerUnreachableException>()
                    .WaitAndRetry(retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                    {

                    }
                    );

                policy.Execute(() =>
                {
                    connection = _connectionFactory.CreateConnection();
                });

                if (IsConnected)
                {
                    connection.ConnectionShutdown += Connection_ConnectionShutdown;
                    connection.CallbackException += Connection_CallbackException;
                    connection.ConnectionBlocked += Connection_ConnectionBlocked;
                    // log
                    return true;
                }

                return false;

                
            }
        }

        private void Connection_ConnectionBlocked(object? sender, global::RabbitMQ.Client.Events.ConnectionBlockedEventArgs e)
        {
            if(_disposed) return;
            TryConnect();
        }

        private void Connection_CallbackException(object? sender, global::RabbitMQ.Client.Events.CallbackExceptionEventArgs e)
        {
            if (_disposed) return;
            TryConnect();
        }

        private void Connection_ConnectionShutdown(object? sender, ShutdownEventArgs e)
        {
            if (_disposed) return;
            //log Connection_ConnectionShutdown

            TryConnect();


        }
    }
}
