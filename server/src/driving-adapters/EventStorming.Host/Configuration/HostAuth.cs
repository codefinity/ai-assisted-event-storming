namespace EventStorming.Host.Configuration;

/// <summary>Names the host gives the authentication schemes, policies and CORS policies it wires up.</summary>
public static class HostAuth
{
    public const string UserScheme = "user";
    public const string UserPolicy = "user";
    public const string WebAppCors = "web-app";
    public const string PublicApiCors = "public-api";
}
