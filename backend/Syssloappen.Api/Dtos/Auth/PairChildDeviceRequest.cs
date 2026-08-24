using System.ComponentModel.DataAnnotations;
using Syssloappen.Api.Authentication;

namespace Syssloappen.Api.Dtos.Auth;

public sealed class PairChildDeviceRequest
{
    [Required]
    [StringLength(ChildPairingCodeService.CodeLength, MinimumLength = ChildPairingCodeService.CodeLength)]
    public string Code { get; init; } = string.Empty;
}
