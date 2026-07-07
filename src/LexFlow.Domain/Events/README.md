# Events

Domain events (implement `LexFlow.Domain.Common.IDomainEvent`) raised by aggregates,
e.g. `HearingOutcomeRecorded`, `InvoiceSent`. Dispatched post-SaveChanges by an
Infrastructure-layer interceptor.
