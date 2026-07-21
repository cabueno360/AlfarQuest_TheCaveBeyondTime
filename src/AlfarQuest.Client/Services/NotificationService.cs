namespace AlfarQuest.Client.Services;

/// <summary>Transient messages — "Profile saved", "The upload was refused".
///
/// A service rather than state on each page, because the thing that knows an
/// action succeeded is usually not the thing that should draw the message, and
/// because a notification must outlive the component that raised it when that
/// component is about to navigate away.</summary>
public sealed class NotificationService
{
    private readonly List<Notification> _active = [];

    public IReadOnlyList<Notification> Active => _active;
    public event Action? Changed;

    public void Success(string message) => Push(NotificationKind.Success, message);
    public void Error(string message) => Push(NotificationKind.Error, message);
    public void Info(string message) => Push(NotificationKind.Info, message);

    public void Dismiss(Guid id)
    {
        if (_active.RemoveAll(n => n.Id == id) > 0) Changed?.Invoke();
    }

    private void Push(NotificationKind kind, string message)
    {
        _active.Add(new Notification(Guid.NewGuid(), kind, message));

        // Three at once is already more than anyone reads; the oldest goes.
        if (_active.Count > 3) _active.RemoveAt(0);

        Changed?.Invoke();
    }
}

public enum NotificationKind { Success, Error, Info }

public readonly record struct Notification(Guid Id, NotificationKind Kind, string Message);
