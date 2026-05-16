namespace Galileo.Chat.Domain.UseCases.Rooms;

public sealed record CreateRoomCommand(string Name);

public sealed record CreateRoomResult(Guid Id, string Name, byte[] Salt, DateTime CreatedAt);
