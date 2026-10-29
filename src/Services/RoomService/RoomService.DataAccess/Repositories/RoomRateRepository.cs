using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RoomService.Business.Exceptions;
using RoomService.Business.Entities;
using RoomService.Business.Interfaces;
using RoomService.DataAccess.Context;

namespace RoomService.DataAccess.Repositories;

public sealed class RoomRateRepository : IRoomRateRepository
{
    private readonly RoomDbContext _context;

    public RoomRateRepository(RoomDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RoomRate>> GetByRoomTypeAsync(int roomTypeId, CancellationToken cancellationToken)
        => await _context.RoomRates
            .AsNoTracking()
            .Include(rate => rate.RoomType)
            .Where(rate => rate.RoomTypeId == roomTypeId)
            .OrderByDescending(rate => rate.ValidFrom)
            .ToListAsync(cancellationToken);

    public async Task<RoomRate?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _context.RoomRates
            .Include(rate => rate.RoomType)
            .FirstOrDefaultAsync(rate => rate.Id == id, cancellationToken);

    public async Task<RoomRate?> GetApplicableAsync(int roomTypeId, DateOnly date, CancellationToken cancellationToken)
        => await _context.RoomRates
            .AsNoTracking()
            .Where(rate => rate.RoomTypeId == roomTypeId && rate.ValidFrom <= date && rate.ValidTo >= date)
            .OrderByDescending(rate => rate.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryAddAsync(RoomRate rate, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await AcquireRoomTypeLockAsync(rate.RoomTypeId, cancellationToken);

        var overlaps = await _context.RoomRates.AnyAsync(
            existing => existing.RoomTypeId == rate.RoomTypeId
                && existing.ValidFrom <= rate.ValidTo
                && existing.ValidTo >= rate.ValidFrom,
            cancellationToken);

        if (overlaps)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        _context.RoomRates.Add(rate);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryUpdateAsync(RoomRate rate, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await AcquireRoomTypeLockAsync(rate.RoomTypeId, cancellationToken);

        var overlaps = await _context.RoomRates.AnyAsync(
            existing => existing.RoomTypeId == rate.RoomTypeId
                && existing.Id != rate.Id
                && existing.ValidFrom <= rate.ValidTo
                && existing.ValidTo >= rate.ValidFrom,
            cancellationToken);

        if (overlaps)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        _context.RoomRates.Update(rate);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task DeleteAsync(RoomRate rate, CancellationToken cancellationToken)
    {
        _context.RoomRates.Remove(rate);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task AcquireRoomTypeLockAsync(int roomTypeId, CancellationToken cancellationToken)
    {
        var transaction = _context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("A database transaction is required to lock room rates.");

        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "sp_getapplock";
        command.CommandType = CommandType.StoredProcedure;
        command.Transaction = transaction.GetDbTransaction();

        AddParameter(command, "@Resource", $"RoomRate:{roomTypeId}", DbType.String);
        AddParameter(command, "@LockMode", "Exclusive", DbType.String);
        AddParameter(command, "@LockOwner", "Transaction", DbType.String);
        AddParameter(command, "@LockTimeout", 10000, DbType.Int32);

        var result = command.CreateParameter();
        result.ParameterName = "@Result";
        result.DbType = DbType.Int32;
        result.Direction = ParameterDirection.ReturnValue;
        command.Parameters.Add(result);

        await command.ExecuteNonQueryAsync(cancellationToken);

        if (result.Value is int code && code < 0)
        {
            throw new ConflictException("The rate period is being updated by another request. Try again.");
        }
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        object value,
        DbType type)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        parameter.DbType = type;
        command.Parameters.Add(parameter);
    }
}
