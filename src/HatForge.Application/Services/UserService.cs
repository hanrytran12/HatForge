using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserDto> CreateAsync(RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var existing = await _unitOfWork.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (existing != null)
            throw new BusinessRuleException("Email already registered");

        if (dto.Role is UserRole.Staff or UserRole.QCWorkshop)
        {
            if (!dto.WorkshopId.HasValue)
                throw new BusinessRuleException("Workshop is required for Staff and QC Workshop users");

            _ = await _unitOfWork.Workshops.GetByIdAsync(dto.WorkshopId.Value)
                ?? throw new NotFoundException("Workshop not found");
        }
        else if (dto.WorkshopId.HasValue)
        {
            throw new BusinessRuleException("Workshop can only be assigned to Staff or QC Workshop users");
        }

        var user = new User
        {
            Email = email,
            Name = dto.Name.Trim(),
            Role = dto.Role,
            WorkshopId = dto.WorkshopId,
            IsActive = true,
            PasswordHash = _passwordHasher.Hash(dto.Password)
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return await MapToDtoAsync(user.Id);
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync()
    {
        var users = await _unitOfWork.Users.FindAsync(x => x.IsActive, new[] { "Workshop" });
        return users
            .OrderBy(x => x.Role)
            .ThenBy(x => x.Name)
            .Select(MapToDto)
            .ToList();
    }

    public async Task DeleteStaffAsync(int id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id)
            ?? throw new NotFoundException("User not found");

        if (!user.IsActive)
            throw new NotFoundException("User not found");

        if (user.Role != UserRole.Staff)
            throw new BusinessRuleException("Only Staff users can be deleted from this endpoint");

        if (user.WorkshopId.HasValue && await WorkshopHasActiveWorkAsync(user.WorkshopId.Value))
            throw new BusinessRuleException("Cannot delete staff while their workshop is working on an active production batch");

        user.IsActive = false;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<bool> WorkshopHasActiveWorkAsync(int workshopId)
    {
        var batchWorkshops = await _unitOfWork.BatchWorkshops.FindAsync(
            x => x.WorkshopId == workshopId && !x.IsCompleted,
            new[] { "Batch" });

        foreach (var bw in batchWorkshops)
        {
            if (bw.Batch == null || !IsActiveProductionStatus(bw.Batch.Status))
                continue;

            if (!await HasWorkshopTurnStartedAsync(bw))
                continue;

            var existingWork = await _unitOfWork.Works.FirstOrDefaultAsync(
                x => x.BatchId == bw.BatchId && x.WorkshopId == workshopId);
            if (existingWork != null)
                return true;
        }

        return false;
    }

    private async Task<bool> HasWorkshopTurnStartedAsync(BatchWorkshop bw)
    {
        if (bw.OrderIndex == 0)
            return true;

        var previousBw = await _unitOfWork.BatchWorkshops.FirstOrDefaultAsync(
            x => x.BatchId == bw.BatchId && x.OrderIndex == bw.OrderIndex - 1);
        if (previousBw == null)
            return false;

        var transfer = await _unitOfWork.TransferRequests.FirstOrDefaultAsync(
            x => x.BatchId == bw.BatchId
              && x.FromWorkshopId == previousBw.WorkshopId
              && x.ToWorkshopId == bw.WorkshopId
              && x.Status == TransferStatus.Transferred);

        return transfer != null;
    }

    private static bool IsActiveProductionStatus(BatchStatus status) =>
        status is BatchStatus.InProduction or BatchStatus.UnderQCReview or BatchStatus.ReadyForTransfer;

    private async Task<UserDto> MapToDtoAsync(int userId)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(x => x.Id == userId, new[] { "Workshop" })
            ?? throw new NotFoundException("User not found");
        return MapToDto(user);
    }

    private static UserDto MapToDto(User user) => new(
        user.Id,
        user.Email,
        user.Name,
        user.Role.ToString(),
        user.WorkshopId,
        user.Workshop?.Name,
        user.IsActive
    );
}
