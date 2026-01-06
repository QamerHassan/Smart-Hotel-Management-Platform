using System.Collections.Concurrent;
using SmartHotel.Domain.Interfaces;

namespace SmartHotel.Infrastructure.Services;

public class InMemoryBookingLockService : IBookingLockService
{
    // A dictionary to hold a semaphore for each room.
    // SemaphoreSlim(1, 1) acts as a mutex.
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _roomLocks = new();

    public async Task<bool> TryAcquireLockAsync(int roomId)
    {
        var semaphore = _roomLocks.GetOrAdd(roomId, _ => new SemaphoreSlim(1, 1));

        // Try to enter the semaphore with a timeout (e.g., 2 seconds).
        // If we can't enter in 2 seconds, assume it's busy and fail.
        return await semaphore.WaitAsync(TimeSpan.FromSeconds(2));
    }

    public void ReleaseLock(int roomId)
    {
        if (_roomLocks.TryGetValue(roomId, out var semaphore))
        {
            try
            {
                semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // Lock was already free or double-released. Ignore.
            }
        }
    }
}
