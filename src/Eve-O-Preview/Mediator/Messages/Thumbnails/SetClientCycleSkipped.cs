using MediatR;

namespace EveOPreview.Mediator.Messages;

public sealed record SetClientCycleSkipped(string Title, bool Skipped) : IRequest;
