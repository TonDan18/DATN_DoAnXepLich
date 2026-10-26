using System.Collections.Concurrent;

namespace XepLichGiangDay.Api.Services.ThuatToan;

/// <summary>
/// Quản lý khóa đồng thời theo từng học kỳ.
/// Đảm bảo tại một thời điểm chỉ có duy nhất 1 tiến trình xếp lịch chạy cho 1 học kỳ (tránh race condition).
/// </summary>
public class HocKyLockService
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    public SemaphoreSlim GetLock(int maHocKy)
    {
        return _locks.GetOrAdd(maHocKy, _ => new SemaphoreSlim(1, 1));
    }

    public bool DangChay(int maHocKy)
    {
        if (_locks.TryGetValue(maHocKy, out var sem))
        {
            return sem.CurrentCount == 0;
        }
        return false;
    }
}
