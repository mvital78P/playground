using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace DriveSearch.Infrastructure.Drive;

/// <summary>
/// Handles Google OAuth2 authentication and credential management.
/// Stores tokens in token.json for refresh capability.
/// </summary>
public class GoogleAuthService
{
    private readonly string _credentialsPath;
    private readonly string _tokenPath;
    private readonly string[] _scopes;

    /// <summary>
    /// Default OAuth2 scopes for Google Drive read-only access.
    /// </summary>
    public static readonly string[] DefaultScopes = { DriveService.Scope.DriveReadonly };

    public GoogleAuthService(
        string credentialsPath = "credentials.json",
        string tokenPath = "token.json",
        string[]? scopes = null)
    {
        _credentialsPath = credentialsPath;
        _tokenPath = tokenPath;
        _scopes = scopes ?? DefaultScopes;
    }

    /// <summary>
    /// Gets or refreshes OAuth2 credentials.
    /// Opens browser for first-time authorization.
    /// Caches token in token.json for subsequent use.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>UserCredential for API access</returns>
    public async Task<UserCredential> GetCredentialsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_credentialsPath))
        {
            throw new FileNotFoundException(
                $"Google credentials file not found: {_credentialsPath}. " +
                "Download from Google Cloud Console.");
        }

        using var stream = new FileStream(_credentialsPath, FileMode.Open, FileAccess.Read);

        // Get token directory from token path
        var tokenDirectory = Path.GetDirectoryName(_tokenPath) ?? ".";
        var tokenFileName = Path.GetFileName(_tokenPath);

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            _scopes,
            "user",
            cancellationToken,
            new FileDataStore(tokenDirectory, true)
        );

        // Rename token file if needed (FileDataStore may use different name)
        var generatedTokenPath = Path.Combine(tokenDirectory, "Google.Apis.Auth.OAuth2.Responses.TokenResponse-user");
        if (File.Exists(generatedTokenPath) && !File.Exists(_tokenPath))
        {
            File.Move(generatedTokenPath, _tokenPath);
        }

        return credential;
    }

    /// <summary>
    /// Creates an authenticated DriveService instance.
    /// </summary>
    /// <param name="applicationName">Application name for API requests</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configured DriveService</returns>
    public async Task<DriveService> CreateDriveServiceAsync(
        string applicationName = "DriveSearch.Net",
        CancellationToken cancellationToken = default)
    {
        var credential = await GetCredentialsAsync(cancellationToken);

        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = applicationName
        });
    }
}
