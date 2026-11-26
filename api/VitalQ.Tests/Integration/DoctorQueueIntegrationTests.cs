using System.Net;
using System.Text;
using System.Text.Json;
using VitalQ.Entities.DTOs;

namespace VitalQ.Tests.Integration;

public class DoctorQueueIntegrationTests : IClassFixture<VitalQWebApplicationFactory>
{
    private readonly VitalQWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DoctorQueueIntegrationTests(VitalQWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DoctorQueueWorkflow_CallNext_SummonsPatient_DischargeWithNotes_Succeeds()
    {
        // 1. Authenticate as Doctor
        var doctorClient = await _factory.CreateAuthenticatedClientAsync("dr.smith", "DoctorPass123!");

        // 2. Call Next Patient (Waiting -> Called)
        var callNextResponse = await doctorClient.PostAsync($"/api/doctor/call-next/{_factory.DoctorId}", null);
        callNextResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var callNextJson = await callNextResponse.Content.ReadAsStringAsync();
        var calledToken = JsonSerializer.Deserialize<QueueTokenResponse>(callNextJson, _jsonOptions);

        calledToken.Should().NotBeNull();
        calledToken!.Id.Should().Be(_factory.WaitingTokenId);
        calledToken.Status.Should().Be("Called");
        calledToken.TokenNumber.Should().Be("CARD-001");

        // 3. Clinical Safety Guard: Discharge without notes (under 5 chars) returns 400 Bad Request
        var shortNotesPayload = new CompleteConsultationRequest
        {
            ConsultationNotes = "Ok" // 2 characters (fails >= 5 requirement)
        };
        var shortNotesContent = new StringContent(
            JsonSerializer.Serialize(shortNotesPayload),
            Encoding.UTF8,
            "application/json");

        var badDischargeResponse = await doctorClient.PostAsync(
            $"/api/doctor/tokens/{_factory.WaitingTokenId}/complete",
            shortNotesContent);

        badDischargeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Discharge notes under 5 characters must be rejected with HTTP 400 Bad Request.");

        // 4. Finalize Consultation with valid clinical notes (Called -> Completed)
        var validNotesPayload = new CompleteConsultationRequest
        {
            ConsultationNotes = "Patient assessed. Normal sinus rhythm confirmed on ECG. Prescribed lifestyle modification."
        };
        var validNotesContent = new StringContent(
            JsonSerializer.Serialize(validNotesPayload),
            Encoding.UTF8,
            "application/json");

        var completeResponse = await doctorClient.PostAsync(
            $"/api/doctor/tokens/{_factory.WaitingTokenId}/complete",
            validNotesContent);

        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completeJson = await completeResponse.Content.ReadAsStringAsync();
        var completedToken = JsonSerializer.Deserialize<QueueTokenResponse>(completeJson, _jsonOptions);

        completedToken.Should().NotBeNull();
        completedToken!.Status.Should().Be("Completed");
        completedToken.CompletedAtUtc.Should().NotBeNull();
    }
}
