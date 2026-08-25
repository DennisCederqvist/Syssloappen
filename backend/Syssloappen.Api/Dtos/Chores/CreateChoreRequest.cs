using System.ComponentModel.DataAnnotations;

namespace Syssloappen.Api.Dtos.Chores;

public sealed class CreateChoreRequest
{
    [Required]
    [StringLength(100)]
    public string Title { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }

    public int Points { get; init; } = 5;
}
