using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;

namespace SpotiSharpBackend;

// Drop-in replacement for SpotifyAPI.Web's PKCEAuthenticator. That class unconditionally does
// `InitialToken.RefreshToken = refreshed.RefreshToken;` on every silent refresh. Spotify is
// allowed to omit refresh_token on a refresh response (meaning "keep using the old one" per
// OAuth spec); when it does, the library overwrites a good refresh token with null, and every
// request after that throws "String is empty or null (Parameter 'refreshToken')" forever, since
// there's no way to repair the authenticator's internal state from outside once that happens.
public class SelfHealingPkceAuthenticator : IAuthenticator
{
    private readonly string _clientId;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public PKCETokenResponse Token { get; }

    public event EventHandler<PKCETokenResponse>? TokenRefreshed;

    public SelfHealingPkceAuthenticator(string clientId, PKCETokenResponse initialToken)
    {
        _clientId = clientId;
        Token = initialToken;
    }

    public async Task Apply(IRequest request, IAPIConnector apiConnector)
    {
        if (Token.IsExpired)
        {
            await _refreshGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Token.IsExpired) await RefreshAsync(apiConnector).ConfigureAwait(false);
            }
            finally
            {
                _refreshGate.Release();
            }
        }

        request.Headers["Authorization"] = $"{Token.TokenType} {Token.AccessToken}";
    }

    private async Task RefreshAsync(IAPIConnector apiConnector)
    {
        var previousRefreshToken = Token.RefreshToken;
        var refreshed = await OAuthClient.RequestToken(
            new PKCETokenRefreshRequest(_clientId, previousRefreshToken), apiConnector).ConfigureAwait(false);

        Token.AccessToken = refreshed.AccessToken;
        Token.CreatedAt = refreshed.CreatedAt;
        Token.ExpiresIn = refreshed.ExpiresIn;
        Token.Scope = refreshed.Scope;
        Token.TokenType = refreshed.TokenType;
        Token.RefreshToken = string.IsNullOrEmpty(refreshed.RefreshToken) ? previousRefreshToken : refreshed.RefreshToken;

        TokenRefreshed?.Invoke(this, Token);
    }
}
