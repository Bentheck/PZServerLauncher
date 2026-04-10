namespace PZServerLauncher.Contracts.Runtime;

public sealed record BootstrapOwnerRequestDto(
    string UserName,
    string Password);
