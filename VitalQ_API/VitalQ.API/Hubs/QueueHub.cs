using Microsoft.AspNetCore.SignalR;

namespace VitalQ.API.Hubs;

/// <summary>
/// Real-time SignalR WebSocket hub for PC and Mobile browser clients.
/// Route: /hubs/queue
/// Exclusively manages connection groups (rooms) with zero business logic.
/// </summary>
public class QueueHub : Hub
{
    // Doctor consultation room PC joins doctor group
    public async Task JoinDoctorGroup(string doctorId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"doctor-{doctorId}");
    }

    public async Task LeaveDoctorGroup(string doctorId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"doctor-{doctorId}");
    }

    // Nurse triage station PC joins nurse group
    public async Task JoinNurseStationGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "nurse-station");
    }

    public async Task LeaveNurseStationGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "nurse-station");
    }

    // Patient mobile phone or PC browser joins their individual token group for live chime/call alerts
    public async Task JoinTokenGroup(string tokenId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"patient-{tokenId}");
    }

    public async Task LeaveTokenGroup(string tokenId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"patient-{tokenId}");
    }
}
