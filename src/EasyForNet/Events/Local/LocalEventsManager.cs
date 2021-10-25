﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.Events.Local
{
    public class LocalEventsManager : ILocalEventsManager
    {
        private readonly IServiceProvider _serviceProvider;

        public LocalEventsManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task RaiseAsync<TEvent>(TEvent @event)
            where TEvent : class
        {
            var eventHandlers = _serviceProvider.GetServices<ILocalEventHandler<TEvent>>();
            foreach (var eventHandler in eventHandlers)
            {
                await eventHandler.HandleAsync(@event);
            }
        }
    }
}