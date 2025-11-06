using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using QuickMCP.Authentication;
using Shouldly;

namespace QuickMCP.Tests;

public class OAuthGrantTypeAuthenticatorTests
{
    private const string TestTokenUrl = "https://localhost:44322/connect/token";
    private const string TestApiKey = "sk_test_api_key_12345";

    [Fact]
    public void Create_WithValidSettings_ShouldSucceed()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            ["tokenUrl"] = TestTokenUrl,
            ["grantType"] = "api_key",
            ["api_key"] = TestApiKey,
            ["scope"] = "Sneakinn"
        };

        // Act
        var authenticator = OAuthGrantTypeAuthenticator.Create(settings);

        // Assert
        authenticator.ShouldNotBeNull();
        authenticator.Type.ShouldBe("oAuthGrantType");
    }

    [Fact]
    public void Create_WithoutTokenUrl_ShouldThrowException()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            ["grantType"] = "api_key",
            ["api_key"] = TestApiKey
        };

        // Act & Assert
        Should.Throw<ArgumentException>(() => OAuthGrantTypeAuthenticator.Create(settings))
            .Message.ShouldContain("tokenUrl");
    }

    [Fact]
    public void Create_WithoutGrantType_ShouldThrowException()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            ["tokenUrl"] = TestTokenUrl,
            ["api_key"] = TestApiKey
        };

        // Act & Assert
        Should.Throw<ArgumentException>(() => OAuthGrantTypeAuthenticator.Create(settings))
            .Message.ShouldContain("grantType");
    }

    [Fact]
    public async Task AuthenticateRequestAsync_WithValidApiKey_ShouldAddBearerToken()
    {
        // Arrange
        var mockResponse = new
        {
            access_token = "test_jwt_token_12345",
            token_type = "Bearer",
            expires_in = 3600
        };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var authenticator = new OAuthGrantTypeAuthenticator(
            TestTokenUrl,
            "api_key",
            new Dictionary<string, string> { ["api_key"] = TestApiKey },
            "Sneakinn"
        );

        // Use reflection to inject the mock HttpClient
        var httpClientField = typeof(OAuthGrantTypeAuthenticator)
            .GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        httpClientField?.SetValue(authenticator, httpClient);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:44322/api/app/order");

        // Act
        await authenticator.AuthenticateRequestAsync(request);

        // Assert
        request.Headers.Authorization.ShouldNotBeNull();
        request.Headers.Authorization.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe("test_jwt_token_12345");
    }

    [Fact]
    public async Task GetAuthHeadersAsync_WithValidApiKey_ShouldReturnBearerToken()
    {
        // Arrange
        var mockResponse = new
        {
            access_token = "test_jwt_token_67890",
            token_type = "Bearer",
            expires_in = 3600
        };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var authenticator = new OAuthGrantTypeAuthenticator(
            TestTokenUrl,
            "api_key",
            new Dictionary<string, string> { ["api_key"] = TestApiKey },
            "Sneakinn"
        );

        // Use reflection to inject the mock HttpClient
        var httpClientField = typeof(OAuthGrantTypeAuthenticator)
            .GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        httpClientField?.SetValue(authenticator, httpClient);

        // Act
        var headers = await authenticator.GetAuthHeadersAsync();

        // Assert
        headers.ShouldNotBeNull();
        headers.ContainsKey("Authorization").ShouldBeTrue();
        headers["Authorization"].ShouldBe("Bearer test_jwt_token_67890");
    }

    [Fact]
    public async Task IsAuthenticatedAsync_WithValidApiKey_ShouldReturnTrue()
    {
        // Arrange
        var mockResponse = new
        {
            access_token = "test_jwt_token_authenticated",
            token_type = "Bearer",
            expires_in = 3600
        };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var authenticator = new OAuthGrantTypeAuthenticator(
            TestTokenUrl,
            "api_key",
            new Dictionary<string, string> { ["api_key"] = TestApiKey },
            "Sneakinn"
        );

        // Use reflection to inject the mock HttpClient
        var httpClientField = typeof(OAuthGrantTypeAuthenticator)
            .GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        httpClientField?.SetValue(authenticator, httpClient);

        // Act
        var isAuthenticated = await authenticator.IsAuthenticatedAsync();

        // Assert
        isAuthenticated.ShouldBeTrue();
    }

    [Fact]
    public void GetMetadata_ShouldReturnCorrectInformation()
    {
        // Act
        var metadata = OAuthGrantTypeAuthenticator.GetMetadata();

        // Assert
        metadata.ShouldNotBeNull();
        metadata.Type.ShouldBe("oAuthGrantType");
        metadata.Name.ShouldBe("OAuth 2.0 Custom Grant Type Authentication");
        metadata.ConfigKeys.ShouldContain(k => k.Key == "tokenUrl" && k.IsRequired);
        metadata.ConfigKeys.ShouldContain(k => k.Key == "grantType" && k.IsRequired);
        metadata.ConfigKeys.ShouldContain(k => k.Key == "scope" && !k.IsRequired);
    }

    [Fact]
    public async Task AuthenticateRequestAsync_WithPhoneOtpGrant_ShouldSendCorrectParameters()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;

        var mockResponse = new
        {
            access_token = "test_jwt_token_phone_otp",
            token_type = "Bearer",
            expires_in = 3600
        };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var authenticator = new OAuthGrantTypeAuthenticator(
            TestTokenUrl,
            "phone_otp",
            new Dictionary<string, string>
            {
                ["phone_number"] = "+1234567890",
                ["otp"] = "123456",
                ["otp_id"] = "otp_session_123"
            },
            "Sneakinn"
        );

        // Use reflection to inject the mock HttpClient
        var httpClientField = typeof(OAuthGrantTypeAuthenticator)
            .GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        httpClientField?.SetValue(authenticator, httpClient);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:44322/api/app/rider");

        // Act
        await authenticator.AuthenticateRequestAsync(request);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Method.ShouldBe(HttpMethod.Post);
        capturedRequest.RequestUri?.ToString().ShouldBe(TestTokenUrl);

        var content = await capturedRequest.Content!.ReadAsStringAsync();
        content.ShouldContain("grant_type=phone_otp");
        content.ShouldContain("phone_number=%2B1234567890");
        content.ShouldContain("otp=123456");
        content.ShouldContain("otp_id=otp_session_123");
        content.ShouldContain("scope=Sneakinn");
    }

    [Fact]
    public async Task AuthenticateRequestAsync_TokenCaching_ShouldReuseToken()
    {
        // Arrange
        var callCount = 0;

        var mockResponse = new
        {
            access_token = "cached_token_12345",
            token_type = "Bearer",
            expires_in = 3600
        };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback(() => callCount++)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var authenticator = new OAuthGrantTypeAuthenticator(
            TestTokenUrl,
            "api_key",
            new Dictionary<string, string> { ["api_key"] = TestApiKey },
            "Sneakinn"
        );

        // Use reflection to inject the mock HttpClient
        var httpClientField = typeof(OAuthGrantTypeAuthenticator)
            .GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        httpClientField?.SetValue(authenticator, httpClient);

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://localhost:44322/api/app/order");
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://localhost:44322/api/app/order");

        // Act
        await authenticator.AuthenticateRequestAsync(request1);
        await authenticator.AuthenticateRequestAsync(request2);

        // Assert
        callCount.ShouldBe(1); // Should only call token endpoint once due to caching
        request1.Headers.Authorization?.Parameter.ShouldBe("cached_token_12345");
        request2.Headers.Authorization?.Parameter.ShouldBe("cached_token_12345");
    }
}
