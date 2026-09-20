using ReelForge.Core;
using ReelForge.Infrastructure;
using ReelForge.Models;

namespace ReelForge.Services;

public sealed class JobManager
{
    private readonly RenderEngine _engine;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private RenderState _state = new();

    public JobManager(AppPaths paths) => _engine = new RenderEngine(paths);

    public RenderState State
    {
        get
        {
            lock (_gate) return _state.Clone();
        }
    }

    public bool Start(Project project, string kind)
    {
        lock (_gate)
        {
            if (_state.Status == "running") return false;
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _state = new RenderState
            {
                Kind = kind,
                Status = "running",
                Progress = 0,
                Started = DateTime.UtcNow
            };
            _ = RunAsync(project, kind, _cts.Token);
            return true;
        }
    }

    public void Cancel()
    {
        lock (_gate) _cts?.Cancel();
    }

    private async Task RunAsync(Project project, string kind, CancellationToken ct)
    {
        try
        {
            var result = await _engine.RenderAsync(project, kind, p =>
            {
                lock (_gate) _state.Progress = Math.Clamp(p, 0, 1);
            }, ct);

            lock (_gate)
            {
                _state.Status = "done";
                _state.Progress = 1;
                _state.Result = result;
            }
        }
        catch (OperationCanceledException)
        {
            lock (_gate) _state.Status = "cancelled";
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _state.Status = "error";
                _state.Error = ex.Message;
            }
        }
        finally
        {
            lock (_gate)
            {
                _cts?.Dispose();
                _cts = null;
            }
        }
    }
}

public sealed class RenderState
{
    public string Kind { get; set; } = "";
    public string Status { get; set; } = "idle";
    public double Progress { get; set; }
    public DateTime Started { get; set; } = DateTime.UtcNow;
    public object? Result { get; set; }
    public string? Error { get; set; }
    public double Elapsed => Math.Max(0, (DateTime.UtcNow - Started).TotalSeconds);

    public RenderState Clone() => new()
    {
        Kind = Kind,
        Status = Status,
        Progress = Progress,
        Started = Started,
        Result = Result,
        Error = Error
    };
}
