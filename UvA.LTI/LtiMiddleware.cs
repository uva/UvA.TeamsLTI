using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace UvA.LTI;

public class LtiMiddleware
{
    private readonly ILogger<LtiMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly LtiOptions _options;

    private SymmetricSecurityKey SigningKey => new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_options.SigningKey));

    public LtiMiddleware(LtiOptions options, ILogger<LtiMiddleware> logger, RequestDelegate next)
    {
        _logger = logger;
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == $"/{_options.InitiationEndpoint}" && await HandleInitiation(context))
            return;
        if (context.Request.Path == $"/{_options.LoginEndpoint}" && await HandleLogin(context))
            return;
        else
            await _next(context);
    }

    private async Task<bool> HandleLogin(HttpContext context)
    {
        var handler = new JwtSecurityTokenHandler();
        var state = handler.ValidateToken(context.Request.Form["state"][0], new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidIssuer = "lti",
            IssuerSigningKey = SigningKey
        }, out _);

        if (state.FindFirstValue("clientId") != _options.ClientId)
            return false;

        var client = new HttpClient();
        var keyset = new JsonWebKeySet(await client.GetStringAsync(_options.JwksUrl));
        
        var id = handler.ValidateToken(context.Request.Form["id_token"][0], new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            IssuerSigningKeys = keyset.Keys
        }, out _);

        if (state.FindFirstValue("nonce") != id.FindFirstValue("nonce"))
            return false;

        var target = state.FindFirstValue("target");
        if (target == null)
        {
            _logger.LogError("Redirect target missing");
            throw new Exception();
        }

        var jsonOptions = new JsonSerializerOptions {PropertyNamingPolicy = JsonNamingPolicy.CamelCase};
        
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Expires = DateTime.UtcNow.AddMinutes(_options.TokenLifetime),
            Issuer = "lti",
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha512Signature),
            Claims = _options.ClaimsMapping(new LtiPrincipal
            {
                Email = id.FindFirstValue(ClaimTypes.Email),
                NameIdentifier = id.FindFirstValue(ClaimTypes.NameIdentifier),
                Context = JsonSerializer.Deserialize<Context>(id.FindFirstValue(LtiClaimTypes.Context), jsonOptions),
                Roles = id.FindAll(LtiClaimTypes.Roles).Select(c => c.Value).ToArray(),
                CustomClaims = id.FindFirstValue(LtiClaimTypes.Custom) == null ? null 
                    : JsonDocument.Parse(id.FindFirstValue(LtiClaimTypes.Custom)).RootElement
            })
        });

        context.Response.Redirect($"{_options.RedirectUrl ?? target}/#{handler.WriteToken(token)}");
        return true;
    }

    private async Task<bool> HandleInitiation(HttpContext context)
    {
        if (!context.Request.Form["target_link_uri"].Any())
        {
            _logger.LogError("Missing target link uri");
            return false;
        }

        if (context.Request.Form["client_id"][0] != _options.ClientId)
            return false;

        var nonce = Guid.NewGuid().ToString();
        
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Expires = DateTime.UtcNow.AddMinutes(3),
            Issuer = "lti",
            Claims = new Dictionary<string, object>
            {
                ["nonce"] = nonce,
                ["target"] = context.Request.Form["target_link_uri"][0],
                ["clientId"] = _options.ClientId
            },
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha512Signature)
        });
        var state = handler.WriteToken(token);

        var pars = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "id_token",
            ["response_mode"] = "form_post",
            ["redirect_uri"] = $"{context.Request.Scheme}://{context.Request.Host}/{_options.LoginEndpoint}",
            ["login_hint"] = context.Request.Form["login_hint"],
            ["scope"] = "openid",
            ["state"] = state,
            ["nonce"] = nonce,
            ["prompt"] = "none",
            ["lti_message_hint"] = context.Request.Form["lti_message_hint"]
        };
        await Results.Content($@"<html>
<head>
    <title>Working...</title>
</head>
<body>
    <form method='POST' name='hiddenform' action='{_options.AuthenticateUrl}'>
        {string.Join('\n', pars.Select(p => $"<input type='hidden' name={p.Key} value='{p.Value}' />"))}
        <noscript><p>Script is disabled. Click Submit to continue.</p><input type='submit' value='Submit' /></noscript>
    </form>
    <script language='javascript'>
        window.setTimeout(function() {{ document.forms[0].submit(); }}, 0);
    </script>
</body>
</html>", "text/html").ExecuteAsync(context);
        return true;
    }
}

public static class LtiMiddlewareExtensions
{
    public static IApplicationBuilder UseLti(this IApplicationBuilder builder, 
        LtiOptions options) =>
        builder.UseMiddleware<LtiMiddleware>(options);
}