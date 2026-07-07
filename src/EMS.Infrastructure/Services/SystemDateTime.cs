using EMS.Application.Common.Interfaces;

namespace EMS.Infrastructure.Services;

public sealed class SystemDateTime : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}
