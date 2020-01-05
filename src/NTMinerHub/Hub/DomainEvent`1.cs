﻿using System;

namespace NTMiner.Hub {
    public abstract class DomainEvent<TEntity> : IEvent {
        protected DomainEvent(RouteToPathId routeToPathId, TEntity source) {
            this.Id = Guid.NewGuid();
            this.RouteToPathId = routeToPathId;
            this.Target = source;
            this.BornOn = DateTime.Now;
        }

        public Guid Id { get; private set; }
        public RouteToPathId RouteToPathId { get; private set; }
        public DateTime BornOn { get; private set; }
        public TEntity Target { get; private set; }
    }
}
