using EventBus.Base.Abstraction;
using EventBus.Base.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.Base.SubManagers
{
    public class InMemorySubscriptionEventBusManager : IEventBusSubscriptionManager
    {
        private readonly Dictionary<string,List<SubScriptionInfo>> _handlers;
        private readonly List<Type> _eventTypes;

        public event EventHandler<string> OnEventRemoved;
        public Func<string, string> eventNameGetter;

        public InMemorySubscriptionEventBusManager(Func<string,string> eventNameGetter)
        {
            this.eventNameGetter = eventNameGetter;
            _eventTypes = new List<Type>();
            this._handlers = new Dictionary<string, List<SubScriptionInfo>>();
        }
        public bool IsEmpty => _handlers.Keys.Any();

        //public event EventHandler<string> OnEventRemoved;

        public void AddSubscription<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
        {
            var eventName = GetEventKey<T>();

            AddSubscription(typeof(TH), eventName);

            if(!_eventTypes.Contains(typeof(T)))
            {
                _eventTypes.Add(typeof(T));
            }
        }

        public void AddSubscription(Type handlerType, string eventName)
        {
            if(!HasSubscriptiponsForEvent(eventName))
            {
                _handlers.Add(eventName, new List<SubScriptionInfo>());
            }

            if (_handlers[eventName].Any(s=>s.HandleType == handlerType))
            {
                throw new ArgumentException($"Handler Type {handlerType.Name} allready registered for `{eventName}`", nameof(handlerType));
            }

            _handlers[eventName].Add(SubScriptionInfo.Typed(handlerType));
        }

        public void Clear()
        {
            _handlers.Clear();
        }

        public string GetEventKey<T>()
        {
            string eventName = typeof(T).Name;
            return eventNameGetter(eventName);
        }

        public Type GetEventTypeByName(string eventName) => _eventTypes.SingleOrDefault(s=>s.Name ==  eventName);
       

        public IEnumerable<SubScriptionInfo> GetHandlersForEvents<T>() where T : IntegrationEvent
        {
           var key = GetEventKey<T>();

            return GetHandlersForEvents(key);
        }

        public IEnumerable<SubScriptionInfo> GetHandlersForEvents(string eventName) => _handlers[eventName];
       

        public bool HasSubscriptionsForEvent<T>() where T : IntegrationEvent
        {
            var key = GetEventKey<T>();

            return HasSubscriptiponsForEvent(key);
        }

        public bool HasSubscriptiponsForEvent(string eventName) => _handlers.ContainsKey(eventName);
       

        private void RaiseOnRemoved(string eventName)
        {
            var handler = OnEventRemoved;

            handler?.Invoke(this, eventName);
        }

        private SubScriptionInfo FindSubscriptionToremove<T,TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
        {
            var eventName = GetEventKey<T>();

            return FindSubscriptionToremove(eventName, typeof(TH));
        }

        private SubScriptionInfo FindSubscriptionToremove(string eventName, Type handlerType)
        {
            if(!HasSubscriptiponsForEvent(eventName))
            {
                return null;
            }

            return _handlers[eventName].SingleOrDefault(s => s.HandleType == handlerType);
        }

        public void RemoveHandler(string eventName,SubScriptionInfo subRemoveInfo)
        {
            if(subRemoveInfo != null)
            {
                _handlers[eventName].Remove(subRemoveInfo);

                if (!_handlers[eventName].Any())
                {
                    _handlers.Remove(eventName);
                    var eventType = _eventTypes.SingleOrDefault(s => s.Name == eventName);
                    if(eventType != null)
                    {
                        _eventTypes.Remove(eventType);
                    }

                    RaiseOnRemoved(eventName);
                }
            }
        }


        public void RemoveSubscription<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
        {
            var handlerToRemove = FindSubscriptionToremove<T, TH>();
            var eventName = GetEventKey<T>();
            RemoveHandler(eventName, handlerToRemove);
        }
    }
}
