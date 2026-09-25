using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.BoardModelling.Shared;

public sealed class PositionValidator : AbstractValidator<Position>
{
    public PositionValidator()
    {
        RuleFor(position => position.X)
            .Must(BoardRules.IsCoordinate).WithErrorCode("out-of-range")
            .WithMessage($"x must be a number between -{BoardLimits.MaxCoordinate} and {BoardLimits.MaxCoordinate}.")
            .WithFix("Leave 'position' out entirely to have the element placed automatically.");

        RuleFor(position => position.Y)
            .Must(BoardRules.IsCoordinate).WithErrorCode("out-of-range")
            .WithMessage($"y must be a number between -{BoardLimits.MaxCoordinate} and {BoardLimits.MaxCoordinate}.")
            .WithFix("Leave 'position' out entirely to have the element placed automatically.");
    }
}

public sealed class SizeValidator : AbstractValidator<Size>
{
    public SizeValidator()
    {
        RuleFor(size => size.Width)
            .Must(BoardRules.IsExtent).WithErrorCode("out-of-range")
            .WithMessage($"width must be between {BoardLimits.MinSize} and {BoardLimits.MaxSize}.")
            .WithFix("Leave 'size' out to use the type's default size.");

        RuleFor(size => size.Height)
            .Must(BoardRules.IsExtent).WithErrorCode("out-of-range")
            .WithMessage($"height must be between {BoardLimits.MinSize} and {BoardLimits.MaxSize}.")
            .WithFix("Leave 'size' out to use the type's default size.");
    }
}
