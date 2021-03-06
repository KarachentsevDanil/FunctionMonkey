# Authorization

Function Monkey supports the standard authorization types of Azure Functions and adds support for token validation through the Authorization header - typically for use with OpenID Connect and an access token. If you're using token validation then you need to register a class that is able to verify the token and populate a ClaimsPrincipal.

This functionality is comprised of two discrete parts - token validation and claims authorization.

## Token Validation

Token Validation validates a bearer token and returns a populated ClaimsPrincipal object. The authorization type can be specified per function and a default can be set. In the example below token validation is set as a default, a token validator is registered while one of the functions is set to use anonymous authorization:

    public class FunctionAppConfiguration : IFunctionAppConfiguration
    {
        public void Build(IFunctionHostBuilder builder)
        {
            builder
                .Setup((serviceCollection, commandRegistry) =>
                {
                    commandRegistry.Register<InvoiceQueryHandler>();
                })
                .Authorization(authorization => authorization
                    .AuthorizationDefault(AuthorizationTypeEnum.TokenValidation)
                    .TokenValidator<BearerTokenValidator>()
                )
                .Functions(functions => functions
                    .HttpRoute("Invoice", route => route
                        .HttpFunction<InvoiceQuery>()
                    )
                    .HttpRoute("Version", route => route
                        .HttpFunction<VersionQuery>(AuthorizationTypeEnum.Anonymous))
                );
        }
    }

Validators should implement the _ITokenValidator_ interface as shown in the example below (also available as a [gist](https://gist.github.com/JamesRandall/e83f72f98bde2f6ff973e6ecb81199c8)):

    public class BearerTokenValidator : ITokenValidator
    {
        private static readonly IConfigurationManager<OpenIdConnectConfiguration> ConfigurationManager;

        static BearerTokenValidator()
        {
            string domain = Environment.GetEnvironmentVariable("domain");
            
            string wellKnownEndpoint = $"https://{domain}/.well-known/openid-configuration";
            var documentRetriever = new HttpDocumentRetriever { RequireHttps = wellKnownEndpoint.StartsWith("https://") };
            ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                wellKnownEndpoint,
                new OpenIdConnectConfigurationRetriever(),
                documentRetriever
            );
        }

        public async Task<ClaimsPrincipal> ValidateAsync(string authorizationHeader)
        {
            if (!authorizationHeader.StartsWith("Bearer "))
                return null;
            string bearerToken = authorizationHeader.Substring("Bearer ".Length);

            var config = await ConfigurationManager.GetConfigurationAsync(CancellationToken.None);
            var audience = Environment.GetEnvironmentVariable("audience");

            var validationParameter = new TokenValidationParameters()
            {
                RequireSignedTokens = true,
                ValidAudience = audience,
                ValidateAudience = true,
                ValidIssuer = config.Issuer,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                IssuerSigningKeys = config.SigningKeys
            };

            ClaimsPrincipal result = null;
            var tries = 0;

            while (result == null && tries <= 1)
            {
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    result = handler.ValidateToken(bearerToken, validationParameter, out SecurityToken _);
                }
                catch (SecurityTokenSignatureKeyNotFoundException)
                {
                    // This exception is thrown if the signature key of the JWT could not be found.
                    // This could be the case when the issuer changed its signing keys, so we trigger a 
                    // refresh and retry validation.
                    ConfigurationManager.RequestRefresh();
                    tries++;
                }
                catch (SecurityTokenException)
                {
                    return null;
                }
            }

            return result;
        }
    }

Token validators can also be specified on a per function basis as shown in the example below:

    public class FunctionAppConfiguration : IFunctionAppConfiguration
    {
        public void Build(IFunctionHostBuilder builder)
        {
            builder
                .Setup((serviceCollection, commandRegistry) =>
                {
                    commandRegistry.Register<InvoiceQueryHandler>();
                })
                .Authorization(authorization => authorization
                    .AuthorizationDefault(AuthorizationTypeEnum.TokenValidation)
                    .TokenValidator<BearerTokenValidator>()
                )
                .Functions(functions => functions
                    .HttpRoute("Invoice", route => route
                        .HttpFunction<InvoiceQuery>()
                            .Options(options => options.TokenValidator<AnotherTokenValidator>())
                    )
                    .HttpRoute("Version", route => route
                        .HttpFunction<VersionQuery>()
                    )
                );
        }
    }

## Claims Authorization

Claims Authorization inspects the ClaimsPrincipal object and determines if the user is authorized to access a given route. Authorizers must implement the _IClaimsPrincipalAuthorization_ interface and can be specified as a default (in the Authorization builder), at the route, or at the function level via function options.

An example authorizer is shown below:

    public class AllowClaimsAuthorization : IClaimsPrincipalAuthorization
    {
        public Task<bool> IsAuthorized(ClaimsPrincipal claimsPrincipal, string httpVerb, string url)
        {
            return Task.FromResult(true);
        }
    }

Authorizers should return true if the principal has access to the resource, false it not.

And it is shown specified at the route level in the below Function App configuration block:

    public class FunctionAppConfiguration : IFunctionAppConfiguration
    {
        public void Build(IFunctionHostBuilder builder)
        {
            builder
                .Setup((serviceCollection, commandRegistry) =>
                {
                    commandRegistry.Register<InvoiceQueryHandler>();
                })
                .Authorization(authorization => authorization
                    .AuthorizationDefault(AuthorizationTypeEnum.TokenValidation)
                    .TokenValidator<BearerTokenValidator>()
                )
                .Functions(functions => functions
                    .HttpRoute<AllowClaimsAuthorization>("/Invoice", route => route
                        .HttpFunction<InvoiceQuery>()
                    )
                    .HttpRoute("Version", route => route
                        .HttpFunction<VersionQuery>(AuthorizationTypeEnum.Anonymous))
                );
        }
    }
