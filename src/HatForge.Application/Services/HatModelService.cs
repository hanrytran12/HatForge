using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class HatModelService : IHatModelService
{
    private readonly IUnitOfWork _unitOfWork;

    public HatModelService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<HatModelDto> CreateAsync(CreateHatModelDto dto)
    {
        var code = await GenerateUniqueCodeAsync();
        var name = dto.Name.Trim();
        var description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

        var hatModel = new HatModel
        {
            Code = code,
            Name = name,
            Description = description
        };

        await _unitOfWork.HatModels.AddAsync(hatModel);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(hatModel);
    }

    public async Task<IReadOnlyList<HatModelDto>> GetAllAsync()
    {
        var hatModels = await _unitOfWork.HatModels.ListAllAsync();
        return hatModels
            .OrderBy(x => x.Code)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<HatModelDto> UpdateAsync(int id, UpdateHatModelDto dto)
    {
        var hatModel = await _unitOfWork.HatModels.GetByIdAsync(id)
            ?? throw new NotFoundException("Hat model not found");

        hatModel.Name = dto.Name.Trim();
        hatModel.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

        _unitOfWork.HatModels.Update(hatModel);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(hatModel);
    }

    public async Task DeleteAsync(int id)
    {
        var hatModel = await _unitOfWork.HatModels.GetByIdAsync(id)
            ?? throw new NotFoundException("Hat model not found");

        var linkedBatch = await _unitOfWork.Batches.FirstOrDefaultAsync(x => x.HatModelId == id);
        if (linkedBatch != null)
            throw new BusinessRuleException("Cannot delete hat model because it is used by at least one production batch");

        _unitOfWork.HatModels.Remove(hatModel);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<string> GenerateUniqueCodeAsync()
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"HAT-{today}-";

        var todayHatModels = await _unitOfWork.HatModels.FindAsync(x => x.Code.StartsWith(prefix));
        var sequence = todayHatModels.Count + 1;

        while (true)
        {
            var code = $"{prefix}{sequence:D4}";
            var exists = await _unitOfWork.HatModels.FirstOrDefaultAsync(x => x.Code == code);
            if (exists == null)
                return code;

            sequence++;
        }
    }

    private static HatModelDto MapToDto(HatModel hatModel) => new(
        hatModel.Id,
        hatModel.Code,
        hatModel.Name,
        hatModel.Description,
        hatModel.CreatedAt
    );
}
