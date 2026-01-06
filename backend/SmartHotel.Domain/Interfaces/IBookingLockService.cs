namespace SmartHotel.Domain.Interfaces;

public interface IBookingLockService
{
    /// <summary>
    /// Tries to acquire a lock for a specific room.
    /// Returns true if successful, false if lock is already held (or timed out).
    /// </summary>
    Task<bool> TryAcquireLockAsync(int roomId);

    /// <summary>
    /// Releases the lock for a specific room.
    /// </summary>
    void ReleaseLock(int roomId);
}
