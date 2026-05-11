using System.Collections.Concurrent;

namespace Sgcf.A2a.Protocol;

internal sealed class A2aTaskStore
{
    private readonly ConcurrentDictionary<string, A2aTask> _tasks = new();

    internal void Upsert(A2aTask task) => _tasks[task.Id] = task;

    internal A2aTask? Get(string id) => _tasks.TryGetValue(id, out A2aTask? t) ? t : null;
}
