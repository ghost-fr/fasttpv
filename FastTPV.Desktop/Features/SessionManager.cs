namespace FastTPV.Desktop.Features;

public sealed class SessionManager : IDisposable
{
    private readonly TimeSpan _timeout;
    private DateTime _lastActivity = DateTime.UtcNow;

    public UserSession? CurrentUser { get; private set; }
    public event EventHandler? Expired;

    public SessionManager(TimeSpan timeout) => _timeout = timeout;

    public void Start(UserSession user)
    {
        CurrentUser = user;
        Touch();
    }

    public void Touch() => _lastActivity = DateTime.UtcNow;

    public bool IsExpired => CurrentUser is not null && DateTime.UtcNow - _lastActivity > _timeout;

    public void Check()
    {
        if (IsExpired)
        {
            CurrentUser = null;
            Expired?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => CurrentUser = null;
}
