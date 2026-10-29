namespace RoomService.Business.Authorization;

public static class Roles
{
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Receptionist = "Receptionist";

    public const string OwnerOrManager = Owner + "," + Manager;
}
