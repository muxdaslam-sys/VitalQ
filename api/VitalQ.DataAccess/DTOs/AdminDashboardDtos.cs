namespace VitalQ.Entities.DTOs;

/// <summary>
/// Aggregated KPI metrics for the Admin Dashboard overview.
/// Returned by GET /api/admin/dashboard/stats
/// </summary>
public class AdminDashboardStatsResponse
{
    public int TotalDoctors { get; set; }
    public int AvailableDoctors { get; set; }
    public int TotalStaff { get; set; }
    public int TotalPatients { get; set; }
    public int TotalDepartments { get; set; }
    public int TotalNursingStations { get; set; }
}
