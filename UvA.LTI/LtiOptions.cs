using System.Security.Claims;

namespace UvA.LTI;

public class LtiOptions
{
    /// <summary>
    /// Lti client ID
    /// </summary>
    public string ClientId { get; set; }
    
    /// <summary>
    /// Authentication endpoint url
    /// </summary>
    public string AuthenticateUrl { get; set; }
    
    /// <summary>
    /// Key set url
    /// </summary>
    public string JwksUrl { get; set; }
    
    /// <summary>
    /// JWT signing key, minimum 128-bit
    /// </summary>
    public string SigningKey { get; set; } = "";
    
    /// <summary>
    /// Endpoint that handles initation requests
    /// </summary>
    public string InitiationEndpoint { get; set; } = "oidc";
    
    /// <summary>
    /// Endpoint that handles sign in requests
    /// </summary>
    public string LoginEndpoint { get; set; } = "signin-oidc";

    /// <summary>
    /// Override url for redirect after login
    /// </summary>
    public string? RedirectUrl { get; set; }
    
    /// <summary>
    /// Token lifetime in minutes
    /// </summary>
    public int TokenLifetime { get; set; } = 120;

    /// <summary>
    /// Mapping of LTI properties to claims
    /// </summary>
    public Func<LtiPrincipal, Dictionary<string, object>> ClaimsMapping { get; set; }
        = p => new Dictionary<string, object>
        {
            [ClaimTypes.Email] = p.Email
        };
}