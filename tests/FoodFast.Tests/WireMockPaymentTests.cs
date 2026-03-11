using System.Net;
using System.Net.Http.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace FoodFast.Tests;

/// <summary>
/// Demonstrates WireMock.Net as an out-of-process test double.
/// The real HTTP stack is exercised — our code doesn't know it's talking to a mock.
///
/// KEY DIFFERENCE FROM MOQ:
/// - Moq lives inside your process (in-memory). No network stack.
/// - WireMock starts a REAL HTTP server on a random port.
///   Your HttpClient connects over TCP — serialization, headers, timeouts all tested.
/// </summary>
public class WireMockPaymentTests : IDisposable
{
    private readonly WireMockServer _mockServer;
    private readonly HttpClient _httpClient;

    public WireMockPaymentTests()
    {
        // WireMock starts a REAL HTTP server on a random port
        _mockServer = WireMockServer.Start();
        _httpClient = new HttpClient { BaseAddress = new Uri(_mockServer.Url!) };
    }

    #region 1: STUBBING — PRE-RECORDED RESPONSES

    /// <summary>
    /// Stubbing: define what the fake server returns for a given request.
    /// This verifies our code handles a successful payment response correctly.
    /// </summary>
    [Fact]
    public async Task PaymentApi_WhenChargeSucceeds_Returns200WithTransactionId()
    {
        // Arrange — stub the external payment API
        _mockServer
            .Given(Request.Create()
                .WithPath("/api/payments/charge")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { transactionId = "TXN-12345", status = "approved" }));

        // Act — our code calls the "real" HTTP endpoint
        var response = await _httpClient.PostAsJsonAsync("/api/payments/charge",
            new { customerId = "C001", amount = 25.50 });

        // Assert — verify the HTTP response
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.Equal("TXN-12345", body!.TransactionId);
        Assert.Equal("approved", body.Status);
    }

    #endregion

    #region 2: FAULT INJECTION — SIMULATING FAILURES

    /// <summary>
    /// Fault injection: simulate a payment gateway outage.
    /// With Moq, you'd throw an exception — but that skips the HTTP layer entirely.
    /// WireMock returns a real 503 over the network, including headers and body.
    /// </summary>
    [Fact]
    public async Task PaymentApi_WhenServiceUnavailable_Returns503()
    {
        // Arrange — simulate a payment gateway outage
        _mockServer
            .Given(Request.Create()
                .WithPath("/api/payments/charge")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { error = "Service temporarily unavailable" }));

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/payments/charge",
            new { customerId = "C001", amount = 25.50 });

        // Assert — our code must handle this gracefully
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    /// <summary>
    /// Timeout simulation: WireMock delays the response by 10 seconds,
    /// but our HttpClient has a 2-second timeout. This tests the REAL
    /// network timeout path — something Moq can never simulate because
    /// Moq returns instantly from in-process memory.
    /// </summary>
    [Fact]
    public async Task PaymentApi_WhenNetworkTimeout_ThrowsException()
    {
        // Arrange — simulate a 10-second network delay
        _mockServer
            .Given(Request.Create()
                .WithPath("/api/payments/charge")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithDelay(TimeSpan.FromSeconds(10))
                .WithStatusCode(200));

        // Act & Assert — HttpClient should timeout
        _httpClient.Timeout = TimeSpan.FromSeconds(2);
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            _httpClient.PostAsJsonAsync("/api/payments/charge",
                new { customerId = "C001", amount = 25.50 }));
    }

    #endregion

    #region 3: VERIFICATION — DID OUR CODE CALL THE API CORRECTLY?

    /// <summary>
    /// Verification: WireMock records every request it receives.
    /// We can inspect the log to verify our code sent the correct data.
    /// This is the "Verification" feature from the slides.
    /// </summary>
    [Fact]
    public async Task PaymentApi_VerifyRequestWasMadeWithCorrectBody()
    {
        // Arrange
        _mockServer
            .Given(Request.Create()
                .WithPath("/api/payments/charge")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { transactionId = "TXN-99", status = "approved" }));

        // Act
        await _httpClient.PostAsJsonAsync("/api/payments/charge",
            new { customerId = "C001", amount = 25.50 });

        // Assert — WireMock recorded the request
        var entries = _mockServer.LogEntries;
        Assert.Single(entries);
        Assert.Contains("C001", entries.First().RequestMessage.Body);
        Assert.Contains("25.5", entries.First().RequestMessage.Body);
    }

    #endregion

    public void Dispose()
    {
        _httpClient.Dispose();
        _mockServer.Stop();
        _mockServer.Dispose();
    }

    /// <summary>
    /// Simple DTO for deserializing the payment API response.
    /// </summary>
    private record PaymentResponse(string TransactionId, string Status);
}
