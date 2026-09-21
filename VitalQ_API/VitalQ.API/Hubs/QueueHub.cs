using Microsoft.AspNetCore.SignalR;

namespace VitalQ.API.Hubs;

/// <summary>
/// Real-time SignalR WebSocket hub (§17 SignalR hub contract, Page 17 & 18).
/// Route: /hubs/queue
/// </summary>
public class QueueHub : Hub
{
    public async Task JoinDepartmentGroup(string departmentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"dept-{departmentId}");
    }

    public async Task LeaveDepartmentGroup(string departmentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dept-{departmentId}");
    }

    public async Task JoinDoctorGroup(string doctorId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"doctor-{doctorId}");
    }

    public async Task LeaveDoctorGroup(string doctorId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"doctor-{doctorId}");
    }

    public async Task JoinTokenGroup(string tokenId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"patient-{tokenId}");
    }

    public async Task LeaveTokenGroup(string tokenId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"patient-{tokenId}");
    }
}
