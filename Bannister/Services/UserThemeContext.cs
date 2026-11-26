namespace Bannister.Services;

internal sealed class UserThemeContext(AuthService auth, PrivacyModeService privacy) : IThemeContext
{
    public (string Username, ActivityDisplayMode Mode)? Current => auth.IsLoggedIn
        ? (auth.CurrentUsername, privacy.GetDisplayMode(auth.CurrentUsername)) : null;
}
