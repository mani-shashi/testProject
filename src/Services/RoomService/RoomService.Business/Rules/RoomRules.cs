using RoomService.Business.Exceptions;
using RoomService.Contracts.Enums;

namespace RoomService.Business.Rules;

public static class RoomRules
{
    public static void EnsureStayDates(DateOnly checkIn, DateOnly checkOut)
    {
        if (checkOut <= checkIn)
        {
            throw new ValidationException("Check-out must be after check-in.");
        }
    }

    public static void EnsureInclusiveRange(DateOnly from, DateOnly to, string label)
    {
        if (to < from)
        {
            throw new ValidationException($"{label}: the end date must not be before the start date.");
        }
    }

    public static void EnsureStatusDefined(RoomStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ValidationException("Invalid room status.");
        }
    }

    public static void EnsureAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ValidationException("Amount must be greater than zero.");
        }
    }
}
