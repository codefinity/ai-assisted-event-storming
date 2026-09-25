using EventStorming.Collaboration.Slices.JoinBoard;
using EventStorming.Collaboration.Slices.LeaveBoard;
using EventStorming.Collaboration.Slices.MoveCursor;
using EventStorming.Collaboration.Slices.SetEditingFocus;
using EventStorming.Collaboration.Slices.ShareDragPreview;
using Microsoft.Extensions.DependencyInjection;

namespace EventStorming.Collaboration;

public static class CollaborationServiceExtensions
{
    public static IServiceCollection AddEventStormingCollaboration(this IServiceCollection services)
    {
        services.AddScoped<IJoinBoardCommandHandler, JoinBoardCommandHandler>();
        services.AddScoped<ILeaveBoardCommandHandler, LeaveBoardCommandHandler>();
        services.AddScoped<IMoveCursorCommandHandler, MoveCursorCommandHandler>();
        services.AddScoped<ISetEditingFocusCommandHandler, SetEditingFocusCommandHandler>();
        services.AddScoped<IShareDragPreviewCommandHandler, ShareDragPreviewCommandHandler>();

        return services;
    }
}
